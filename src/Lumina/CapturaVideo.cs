using Lumina.Nucleo;
using Vortice.Direct3D11;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>
/// Lê a placa escolhida numa thread própria e entrega cada quadro à tela.
/// Placa ausente, em uso ou desconectada: espera 2 s e tenta de novo, até Parar.
/// Ao abrir, publica os modos que a placa oferece (para o menu) e o modo em uso (para o título).
/// </summary>
sealed class CapturaVideo : IDisposable
{
    readonly Renderizador _tela;
    readonly Action<double>? _aoQuadro;
    readonly Revezamento _revezamento = new("captura");
    readonly ResumoAtraso _chegada = new();
    long _ultimoQuadro; // Environment.TickCount64; 0 = nenhum ainda
    volatile ProblemaCaptura _problema;
    volatile IReadOnlyList<ModoVideo> _modos = [];
    volatile ModoVideo? _modoAtual;
    volatile DispositivoVideo? _placaAberta;
    // Publicar e zerar o estado (modos, modo, placa aberta) sob a mesma trava em que a thread antiga é avisada:
    // assim uma thread que já ia publicar não sobrescreve o estado zerado da troca (revisão final, 05/10/2026).
    readonly object _travaEstado = new();

    /// <summary>
    /// A placa que esta captura de fato abriu. Pode ter link diferente do pedido: a mesma placa em outra porta USB.
    /// A janela compara para reiniciar o som com o aparelho novo (revisão final, 05/10/2026).
    /// </summary>
    public DispositivoVideo? PlacaAberta => _placaAberta;

    sealed class PlacaNaoConectada() : Exception("placa não conectada");
    sealed class SemModoUtil() : Exception("a câmera não tem modo 16:9 de 720p ou mais");

    /// <summary>Os modos da placa aberta por último (vazio antes de abrir).</summary>
    public IReadOnlyList<ModoVideo> ModosDaPlaca => _modos;

    /// <summary>O modo aberto agora; null antes de abrir.</summary>
    public ModoVideo? ModoAtual => _modoAtual;

    /// <summary>O último problema ao abrir ou ler a placa; Nenhum quando chegou quadro.</summary>
    public ProblemaCaptura Problema => _problema;

    /// <param name="aoQuadro">Recebe o atraso em ms entre a chegada do quadro e o envio dele para a tela.</param>
    public CapturaVideo(Renderizador tela, Action<double>? aoQuadro)
    {
        _tela = tela;
        _aoQuadro = aoQuadro;
    }

    /// <summary>Milissegundos desde o último quadro desenhado; long.MaxValue se nenhum chegou.</summary>
    public long MsDesdeUltimoQuadro
    {
        get
        {
            long t = Interlocked.Read(ref _ultimoQuadro);
            return t == 0 ? long.MaxValue : Environment.TickCount64 - t;
        }
    }

    /// <summary>
    /// Não espera: a thread anterior é avisada e a nova só começa quando ela terminar (abrir leva ~4 s).
    /// Antes, trocar de modo durante a abertura deixava duas capturas disputando a placa (revisão final).
    /// </summary>
    /// <param name="modoDesejado">Nome do modo ("720p60"); se a placa não tiver, Modos.Escolher decide.</param>
    public void Iniciar(DispositivoVideo placa, string? modoDesejado)
    {
        Interlocked.Exchange(ref _ultimoQuadro, 0);
        lock (_travaEstado)
        {
            _revezamento.Iniciar(vez => Laco(placa, modoDesejado, vez)); // avisa a anterior
            // Até a placa nova abrir, menu e título não mostram os modos da anterior (achado do Douglas, 04/10/2026:
            // escolheu 720p30 no menu velho durante a troca e a preferência da MS2109 virou 720p30).
            LimparEstado();
            _problema = ProblemaCaptura.Abrindo;
        }
    }

    void LimparEstado()
    {
        _modos = [];
        _modoAtual = null;
        _placaAberta = null;
    }

    /// <summary>
    /// Só avisa: a thread sai no próximo quadro, e a placa manda quadros mesmo sem sinal (60 fps, medido em 03/10/2026).
    /// Desligar a fonte daqui travava o ReadSample até o limite de 3 s (medido 3 de 3 vezes).
    /// Falso: a thread ainda está presa abrindo a placa — quem chama não deve desmontar a tela.
    /// </summary>
    public bool Parar()
    {
        // A espera fica FORA da trava: a thread precisa dela no fim da abertura, e esperar segurando-a
        // travaria as duas até o limite. O aviso vem antes; a limpeza, sob a trava, depois.
        bool terminou = _revezamento.Parar(TimeSpan.FromSeconds(3));
        lock (_travaEstado)
        {
            LimparEstado(); // o menu não fica com os modos de uma placa que não está mais aberta
            _problema = ProblemaCaptura.Nenhum;
        }
        if (!terminou) Registro.Log("captura: a thread não terminou em 3 s");
        return terminou;
    }

    public void Dispose() => Parar();

    void Laco(DispositivoVideo placa, string? modoDesejado, Revezamento.Vez vez)
    {
        // Placa fora do lugar: tenta de novo a cada 2 s. O log registra a 1ª tentativa e cada problema NOVO,
        // não a mesma linha a cada 2 s (antes ~86 mil linhas por dia com a placa desplugada).
        bool primeira = true;
        ProblemaCaptura? jaRegistrado = null;
        while (!vez.Parar)
        {
            IMFMediaSource? fonte = null;
            IMFSourceReader? leitor = null;
            try
            {
                var relogio = System.Diagnostics.Stopwatch.StartNew();
                if (primeira) Registro.Log($"captura: abrindo {placa.Nome}");
                primeira = false;
                leitor = Abrir(placa, modoDesejado, vez, out fonte);
                Registro.Log($"captura: abriu em {relogio.ElapsedMilliseconds} ms");
                jaRegistrado = null;
                while (!vez.Parar && LerUm(leitor)) { }
            }
            catch (Exception e) when (!vez.Parar)
            {
                var problema = e switch
                {
                    PlacaNaoConectada => ProblemaCaptura.PlacaAusente,
                    SemModoUtil => ProblemaCaptura.SemModoUtil,
                    SharpGen.Runtime.SharpGenException s => Problemas.DeHResult(s.HResult),
                    _ => ProblemaCaptura.Outro,
                };
                if (problema != jaRegistrado) Registro.Erro("captura", e);
                jaRegistrado = problema;
                _problema = problema;
            }
            catch (Exception)
            {
                // Parada pedida no meio da abertura (OperationCanceledException). Não é erro.
            }
            finally
            {
                leitor?.Dispose();
                if (fonte is not null)
                {
                    try { fonte.Shutdown(); } catch (Exception) { }
                    fonte.Dispose();
                }
            }
            if (Problemas.Permanente(_problema))
            {
                // Tentar de novo não resolve (ex.: câmera sem modo útil): espera uma nova escolha no menu,
                // sem reabrir a câmera a cada 2 s nem encher o log.
                while (!vez.Parar) Thread.Sleep(100);
                break;
            }
            for (int i = 0; i < 20 && !vez.Parar; i++) Thread.Sleep(100);
        }
    }

    IMFSourceReader Abrir(DispositivoVideo placa, string? modoDesejado, Revezamento.Vez vez, out IMFMediaSource? fonte)
    {
        fonte = null;
        // Acha a placa de novo a cada abertura: pode ter mudado de porta USB (link novo, mesmo nome e modelo).
        var achada = Placa.Resolver(Dispositivos.Video(), placa.Link, placa.Nome).Placa ?? throw new PlacaNaoConectada();
        var link = achada.Link;
        using var fa = MediaFactory.MFCreateAttributes(2);
        fa.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
        fa.Set(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink, link);
        fonte = MediaFactory.MFCreateDeviceSource(fa);
        if (vez.Parar) throw new OperationCanceledException();

        // Fixa o formato nativo do modo, preferindo MJPG; sem isso o leitor pode escolher NV12 (decodificado na CPU).
        ModoVideo modo;
        List<ModoVideo> modos;
        using (var pd = fonte.CreatePresentationDescriptor())
        {
            pd.GetStreamDescriptorByIndex(0, out _, out IMFStreamDescriptor sd);
            using (sd)
            using (var mth = sd.MediaTypeHandler)
            {
                var formatos = new List<FormatoNativo>();
                for (int i = 0; i < mth.MediaTypeCount; i++)
                {
                    using var t = mth.GetMediaTypeByIndex(i);
                    formatos.Add(Descrever(i, t));
                }
                modos = Modos.Disponiveis(formatos, link);
                modo = Modos.Escolher(modos, modoDesejado) ?? throw new SemModoUtil();
                int indice = Modos.FormatoPara(formatos, modo)!.Value;
                using var escolhido = mth.GetMediaTypeByIndex(indice);
                mth.CurrentMediaType = escolhido;
            }
        }
        if (vez.Parar) throw new OperationCanceledException();

        using var atr = MediaFactory.MFCreateAttributes(4);
        atr.Set(SourceReaderAttributeKeys.D3DManager, _tela.Gerente);
        atr.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, true);
        atr.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
        atr.Set(SinkWriterAttributeKeys.LowLatency, true);
        var leitor = MediaFactory.MFCreateSourceReaderFromMediaSource(fonte, atr);
        try
        {
            // ARGB32 vira textura B8G8R8A8, o mesmo formato da cadeia de imagens: a cópia é direta.
            using var pedido = MediaFactory.MFCreateMediaType();
            pedido.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            pedido.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Argb32);
            MediaFactory.MFSetAttributeSize(pedido, MediaTypeAttributeKeys.FrameSize, (uint)modo.Largura, (uint)modo.Altura);
            leitor.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, pedido);
            if (vez.Parar) throw new OperationCanceledException();
        }
        catch
        {
            leitor.Dispose();
            throw;
        }
        lock (_travaEstado)
        {
            if (vez.Parar)
            {
                leitor.Dispose();
                throw new OperationCanceledException();
            }
            _modos = modos;
            _modoAtual = modo;
            _placaAberta = achada;
        }
        Registro.Log($"captura aberta: {placa.Nome} {modo.Nome}");
        return leitor;
    }

    bool LerUm(IMFSourceReader leitor)
    {
        using var amostra = leitor.ReadSample(SourceReaderIndex.FirstVideoStream, SourceReaderControlFlag.None,
            out _, out SourceReaderFlag flags, out long tempoAmostra);
        if ((flags & (SourceReaderFlag.EndOfStream | SourceReaderFlag.Error)) != 0) return false;
        if (amostra is null) return true;
        using var buffer = amostra.GetBufferByIndex(0);
        using var dxgi = buffer.QueryInterface<IMFDXGIBuffer>();
        using var textura = new ID3D11Texture2D(dxgi.GetResource(typeof(ID3D11Texture2D).GUID));
        if (_tela.Suave)
        {
            _tela.Guardar(textura, dxgi.SubresourceIndex, tempoAmostra);
            // O atraso da fila acompanha o p95 da chegada: cobre os trancos sem esperar à toa.
            if (_chegada.Registrar((Relogio.Agora100ns() - tempoAmostra) / 10_000.0) is { } r)
            {
                _tela.AtrasoMs = AtrasoSuave.De(r);
                if (_aoQuadro is not null)
                    Registro.Diagnostico($"chegada: mediana={r.Mediana:F1} p95={r.P95:F1} máx={r.Maximo:F1} ms → atraso da fila {_tela.AtrasoMs:F0} ms");
            }
        }
        else
        {
            _tela.Apresentar(textura, dxgi.SubresourceIndex);
            _aoQuadro?.Invoke((Relogio.Agora100ns() - tempoAmostra) / 10_000.0);
        }
        Interlocked.Exchange(ref _ultimoQuadro, Environment.TickCount64);
        _problema = ProblemaCaptura.Nenhum;
        return true;
    }

    static FormatoNativo Descrever(int indice, IMFMediaType t)
    {
        var sub = t.GetGUID(MediaTypeAttributeKeys.Subtype);
        MediaFactory.MFGetAttributeSize(t, MediaTypeAttributeKeys.FrameSize, out uint l, out uint a);
        MediaFactory.MFGetAttributeRatio(t, MediaTypeAttributeKeys.FrameRate, out uint n, out uint d);
        string nome = sub == VideoFormatGuids.Mjpg ? "MJPG" : sub == VideoFormatGuids.NV12 ? "NV12" : sub == VideoFormatGuids.YUY2 ? "YUY2" : sub.ToString();
        return new FormatoNativo(indice, nome, (int)l, (int)a, (int)n, (int)d);
    }
}
