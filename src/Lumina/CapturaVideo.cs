using Lumina.Nucleo;
using Vortice.Direct3D11;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>
/// Lê a placa numa thread própria e entrega cada quadro direto na tela, sem fila.
/// Placa ausente, em uso ou desconectada: espera 2 s e tenta de novo, até Parar.
/// </summary>
sealed class CapturaVideo : IDisposable
{
    readonly Renderizador _tela;
    readonly Action? _aoQuadro;
    readonly object _trava = new();
    Thread? _fio;
    volatile bool _parar;
    IMFMediaSource? _fonte;
    long _ultimoQuadro; // Environment.TickCount64; 0 = nenhum ainda

    public CapturaVideo(Renderizador tela, Action? aoQuadro)
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

    public void Iniciar(ModoVideo modo)
    {
        Parar();
        _parar = false;
        Interlocked.Exchange(ref _ultimoQuadro, 0);
        _fio = new Thread(() => Laco(modo)) { IsBackground = true, Name = "captura" };
        _fio.Start();
    }

    /// <summary>Shutdown na fonte destrava um ReadSample parado (placa sem sinal não entrega quadro).</summary>
    public void Parar()
    {
        _parar = true;
        lock (_trava)
        {
            try { _fonte?.Shutdown(); }
            catch (Exception e) { Registro.Erro("captura.parar", e); }
        }
        if (_fio is not null && !_fio.Join(3000)) Registro.Log("captura: a thread não terminou em 3 s");
        _fio = null;
    }

    public void Dispose() => Parar();

    void Laco(ModoVideo modo)
    {
        while (!_parar)
        {
            IMFSourceReader? leitor = null;
            try
            {
                leitor = Abrir(modo);
                while (!_parar && LerUm(leitor)) { }
            }
            catch (Exception e) when (!_parar)
            {
                Registro.Erro("captura", e);
            }
            catch (Exception)
            {
                // Parada pedida: o Shutdown faz o ReadSample falhar. Não é erro.
            }
            finally
            {
                leitor?.Dispose();
                lock (_trava)
                {
                    try { _fonte?.Shutdown(); } catch (Exception) { }
                    _fonte?.Dispose();
                    _fonte = null;
                }
            }
            for (int i = 0; i < 20 && !_parar; i++) Thread.Sleep(100);
        }
    }

    IMFSourceReader Abrir(ModoVideo modo)
    {
        var link = ProcurarPlaca() ?? throw new InvalidOperationException("placa não conectada");
        using var fa = MediaFactory.MFCreateAttributes(2);
        fa.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
        fa.Set(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink, link);
        var fonte = MediaFactory.MFCreateDeviceSource(fa);
        lock (_trava) _fonte = fonte;
        if (_parar) throw new OperationCanceledException();

        // Fixa o formato nativo MJPG do modo; sem isso o leitor pode escolher NV12 (decodificado na CPU).
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
                int indice = Placa.EscolherFormatoNativo(formatos, modo)
                    ?? throw new InvalidOperationException($"a placa não oferece MJPG {modo.Nome}");
                using var escolhido = mth.GetMediaTypeByIndex(indice);
                mth.CurrentMediaType = escolhido;
            }
        }

        using var atr = MediaFactory.MFCreateAttributes(4);
        atr.Set(SourceReaderAttributeKeys.D3DManager, _tela.Gerente);
        atr.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, true);
        atr.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
        atr.Set(SinkWriterAttributeKeys.LowLatency, true);
        var leitor = MediaFactory.MFCreateSourceReaderFromMediaSource(fonte, atr);

        // ARGB32 vira textura B8G8R8A8, o mesmo formato da cadeia de imagens: a cópia é direta.
        using var pedido = MediaFactory.MFCreateMediaType();
        pedido.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        pedido.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Argb32);
        MediaFactory.MFSetAttributeSize(pedido, MediaTypeAttributeKeys.FrameSize, (uint)modo.Largura, (uint)modo.Altura);
        leitor.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, pedido);
        Registro.Log($"captura aberta: {modo.Nome}");
        return leitor;
    }

    bool LerUm(IMFSourceReader leitor)
    {
        using var amostra = leitor.ReadSample(SourceReaderIndex.FirstVideoStream, SourceReaderControlFlag.None,
            out _, out SourceReaderFlag flags, out _);
        if ((flags & (SourceReaderFlag.EndOfStream | SourceReaderFlag.Error)) != 0) return false;
        if (amostra is null) return true;
        using var buffer = amostra.GetBufferByIndex(0);
        using var dxgi = buffer.QueryInterface<IMFDXGIBuffer>();
        using var textura = new ID3D11Texture2D(dxgi.GetResource(typeof(ID3D11Texture2D).GUID));
        _tela.Apresentar(textura, dxgi.SubresourceIndex);
        Interlocked.Exchange(ref _ultimoQuadro, Environment.TickCount64);
        _aoQuadro?.Invoke();
        return true;
    }

    static FormatoNativo Descrever(int indice, IMFMediaType t)
    {
        var sub = t.GetGUID(MediaTypeAttributeKeys.Subtype);
        MediaFactory.MFGetAttributeSize(t, MediaTypeAttributeKeys.FrameSize, out uint l, out uint a);
        MediaFactory.MFGetAttributeRatio(t, MediaTypeAttributeKeys.FrameRate, out uint n, out uint d);
        return new FormatoNativo(indice, sub == VideoFormatGuids.Mjpg ? "MJPG" : sub.ToString(), (int)l, (int)a, (int)n, (int)d);
    }

    static string? ProcurarPlaca()
    {
        var lista = new List<DispositivoVideo>();
        using (var ativos = MediaFactory.MFEnumVideoDeviceSources())
            foreach (var a in ativos) lista.Add(new DispositivoVideo(a.FriendlyName, a.SymbolicLink));
        return Placa.EscolherVideo(lista)?.Link;
    }
}
