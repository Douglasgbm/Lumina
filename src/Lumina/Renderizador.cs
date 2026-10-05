using Lumina.Nucleo;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>
/// Dono da GPU. A cadeia de imagens tem o tamanho do vídeo e o Windows estica para o painel
/// (Scaling.Stretch): copiar o quadro é tudo que se faz por quadro, sem shader.
///
/// Dois jeitos de mostrar:
/// - menor atraso: a captura chama Apresentar e o quadro vai para a tela na hora em que chega;
/// - suave (como o OBS): a captura chama Guardar, o quadro vai para uma fila curta e um relógio
///   preso ao monitor (WaitForVBlank) mostra a cada batida o quadro que já venceu pelo carimbo.
///   Esconde os trancos de chegada da placa (medidos em 03/10/2026) ao custo do atraso da fila.
/// Toda mexida na cadeia e na fila passa pela mesma trava.
/// </summary>
sealed class Renderizador : IDisposable
{
    public ID3D11Device Dispositivo { get; }
    public IMFDXGIDeviceManager Gerente { get; }

    readonly ID3D11DeviceContext _ctx;
    readonly IntPtr _hwnd;
    readonly object _trava = new();
    IDXGISwapChain1? _cadeia;
    uint _largura, _altura;
    Format _formato;

    // Modo suave
    const int Vagas = 6;
    readonly ID3D11Texture2D?[] _vagas = new ID3D11Texture2D?[Vagas];
    readonly long[] _carimboDaVaga = new long[Vagas];
    readonly Stack<int> _livres = new();
    readonly Agenda<int> _agenda = new(Vagas - 1);
    uint _larguraVagas, _alturaVagas;
    Format _formatoVagas;
    Thread? _relogio;
    volatile bool _pararRelogio;
    double _atrasoMs = AtrasoSuave.Inicial;

    /// <summary>Diagnóstico do modo suave: atraso em ms entre o carimbo do quadro e ele ir para a tela.</summary>
    public Action<double>? AoExibir { get; set; }

    public bool Suave => _relogio is not null;

    volatile bool _perdido;

    /// <summary>A GPU foi removida (driver reiniciado/atualizado): a janela precisa recriar tudo.</summary>
    public bool Perdido => _perdido;

    /// <summary>Atraso da fila do modo suave; a captura ajusta pelo que mede de chegada.</summary>
    public double AtrasoMs
    {
        get => Volatile.Read(ref _atrasoMs);
        set => Volatile.Write(ref _atrasoMs, value);
    }

    public Renderizador(IntPtr hwnd)
    {
        _hwnd = hwnd;
        D3D11.D3D11CreateDevice(null, DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
            new[] { FeatureLevel.Level_11_0 }, out ID3D11Device? dispositivo).CheckError();
        Dispositivo = dispositivo ?? throw new InvalidOperationException("sem dispositivo Direct3D 11");
        _ctx = Dispositivo.ImmediateContext;
        // O Media Foundation usa o mesmo dispositivo em outra thread.
        using (var mt = Dispositivo.QueryInterface<ID3D11Multithread>()) mt.SetMultithreadProtected(true);
        Gerente = MediaFactory.MFCreateDXGIDeviceManager();
        Gerente.ResetDevice(Dispositivo).CheckError();
    }

    /// <summary>Menor atraso: o quadro vai para a tela agora.</summary>
    public void Apresentar(ID3D11Texture2D textura, uint subrecurso)
    {
        lock (_trava) MostrarJa(textura, subrecurso);
    }

    // --- modo suave

    public void IniciarSuave()
    {
        if (_relogio is not null) return;
        _pararRelogio = false;
        _relogio = new Thread(LacoDoRelogio) { IsBackground = true, Name = "relógio da tela" };
        _relogio.Start();
        Registro.Log("tela: modo suave");
    }

    public void PararSuave()
    {
        if (_relogio is null) return;
        _pararRelogio = true;
        _relogio.Join(1000);
        _relogio = null;
        lock (_trava)
            foreach (var v in _agenda.Esvaziar()) _livres.Push(v);
        Registro.Log("tela: modo menor atraso");
    }

    /// <summary>Suave: copia o quadro para uma vaga da fila; quem mostra é o relógio.</summary>
    public void Guardar(ID3D11Texture2D textura, uint subrecurso, long carimbo)
    {
        var d = textura.Description;
        lock (_trava)
        {
            if (_vagas[0] is null || d.Width != _larguraVagas || d.Height != _alturaVagas || d.Format != _formatoVagas)
                CriarVagas(d);
            if (!_livres.TryPop(out int vaga)) return; // não acontece: a fila guarda uma vaga a menos que o total
            _ctx.CopySubresourceRegion(_vagas[vaga]!, 0, 0, 0, 0, textura, subrecurso);
            _carimboDaVaga[vaga] = carimbo;
            if (_agenda.Adicionar(carimbo, vaga, out int despejada)) _livres.Push(despejada);
        }
    }

    void LacoDoRelogio()
    {
        IDXGIOutput? monitor = null;
        int batidas = 0;
        try
        {
            while (!_pararRelogio)
            {
                // A janela pode mudar de monitor: pega de novo a cada ~2 s.
                if (monitor is null || ++batidas % 120 == 0)
                {
                    monitor?.Dispose();
                    monitor = null;
                    lock (_trava)
                    {
                        try { monitor = _cadeia?.GetContainingOutput(); }
                        catch (Exception) { monitor = null; }
                    }
                }
                if (monitor is null) Thread.Sleep(5);
                else
                {
                    try { monitor.WaitForVBlank(); }
                    catch (Exception) { monitor.Dispose(); monitor = null; Thread.Sleep(5); }
                }

                lock (_trava)
                {
                    var escolha = _agenda.Escolher(Relogio.Agora100ns(), (long)(AtrasoMs * 10_000));
                    if (escolha is not { } e) continue;
                    foreach (var v in e.Descartados) _livres.Push(v);
                    MostrarJa(_vagas[e.Item]!, 0);
                    _livres.Push(e.Item);
                    AoExibir?.Invoke((Relogio.Agora100ns() - _carimboDaVaga[e.Item]) / 10_000.0);
                }
            }
        }
        catch (Exception ex)
        {
            Registro.Erro("relógio da tela", ex);
        }
        finally
        {
            monitor?.Dispose();
        }
    }

    void CriarVagas(Texture2DDescription origem)
    {
        foreach (var v in _vagas) v?.Dispose();
        _livres.Clear();
        _agenda.Esvaziar();
        var desc = new Texture2DDescription
        {
            Width = origem.Width,
            Height = origem.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = origem.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource,
        };
        for (int i = 0; i < Vagas; i++)
        {
            _vagas[i] = Dispositivo.CreateTexture2D(desc);
            _livres.Push(i);
        }
        _larguraVagas = origem.Width;
        _alturaVagas = origem.Height;
        _formatoVagas = origem.Format;
    }

    // --- comum

    /// <summary>Chamar com a trava.</summary>
    void MostrarJa(ID3D11Texture2D textura, uint subrecurso)
    {
        if (_perdido) return;
        try
        {
            var d = textura.Description;
            if (_cadeia is null || d.Width != _largura || d.Height != _altura || d.Format != _formato)
                CriarCadeia(d.Width, d.Height, d.Format);
            using var fundo = _cadeia!.GetBuffer<ID3D11Texture2D>(0);
            _ctx.CopySubresourceRegion(fundo, 0, 0, 0, 0, textura, subrecurso);
            var r = _cadeia.Present(0, PresentFlags.None);
            if (r.Failure && !VerificarPerda() && !_presentFalhouRegistrado)
            {
                _presentFalhouRegistrado = true; // uma vez: sem isso, tela preta sem nenhuma linha no log
                Registro.Log($"tela: Present falhou ({r})");
            }
        }
        catch (SharpGen.Runtime.SharpGenException)
        {
            if (!VerificarPerda()) throw;
        }
    }

    bool _presentFalhouRegistrado;

    /// <summary>
    /// Confere se a GPU foi removida. Público: no modo "menor atraso" quem falha primeiro é o leitor da placa
    /// (que decodifica na mesma GPU), e o Present nunca é chamado — o vigia pergunta a cada tique.
    /// </summary>
    public bool VerificarPerda()
    {
        if (_perdido) return true;
        var motivo = Dispositivo.DeviceRemovedReason;
        if (motivo.Success) return false;
        _perdido = true;
        Registro.Log($"tela: GPU perdida ({motivo})");
        return true;
    }

    void CriarCadeia(uint largura, uint altura, Format formato)
    {
        _cadeia?.Dispose();
        using var dxgi = Dispositivo.QueryInterface<IDXGIDevice>();
        using var adaptador = dxgi.GetAdapter();
        using var fabrica = adaptador.GetParent<IDXGIFactory2>();
        var desc = new SwapChainDescription1
        {
            Width = largura,
            Height = altura,
            Format = formato,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = new SampleDescription(1, 0),
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
        };
        _cadeia = fabrica.CreateSwapChainForHwnd(Dispositivo, _hwnd, desc);
        fabrica.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAll);
        _largura = largura;
        _altura = altura;
        _formato = formato;
        Registro.Log($"tela: {largura}x{altura} {formato}");
    }

    public void Dispose()
    {
        // Para o relógio sem registrar "modo menor atraso": ao fechar, o modo não mudou.
        _pararRelogio = true;
        _relogio?.Join(1000);
        _relogio = null;
        lock (_trava)
        {
            foreach (var v in _vagas) v?.Dispose();
            _cadeia?.Dispose();
        }
        Gerente.Dispose();
        _ctx.Dispose();
        Dispositivo.Dispose();
    }
}
