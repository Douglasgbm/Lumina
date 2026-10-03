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
    readonly Action<double>? _aoQuadro;
    readonly Revezamento _revezamento = new("captura");
    long _ultimoQuadro; // Environment.TickCount64; 0 = nenhum ainda

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
    public void Iniciar(ModoVideo modo)
    {
        Interlocked.Exchange(ref _ultimoQuadro, 0);
        _revezamento.Iniciar(vez => Laco(modo, vez));
    }

    /// <summary>
    /// Só avisa: a thread sai no próximo quadro, e a placa manda quadros mesmo sem sinal (60 fps, medido em 03/10/2026).
    /// Desligar a fonte daqui travava o ReadSample até o limite de 3 s (medido 3 de 3 vezes).
    /// Falso: a thread ainda está presa abrindo a placa — quem chama não deve desmontar a tela.
    /// </summary>
    public bool Parar()
    {
        bool terminou = _revezamento.Parar(TimeSpan.FromSeconds(3));
        if (!terminou) Registro.Log("captura: a thread não terminou em 3 s");
        return terminou;
    }

    public void Dispose() => Parar();

    void Laco(ModoVideo modo, Revezamento.Vez vez)
    {
        while (!vez.Parar)
        {
            IMFMediaSource? fonte = null;
            IMFSourceReader? leitor = null;
            try
            {
                leitor = Abrir(modo, vez, out fonte);
                while (!vez.Parar && LerUm(leitor)) { }
            }
            catch (Exception e) when (!vez.Parar)
            {
                Registro.Erro("captura", e);
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
            for (int i = 0; i < 20 && !vez.Parar; i++) Thread.Sleep(100);
        }
    }

    IMFSourceReader Abrir(ModoVideo modo, Revezamento.Vez vez, out IMFMediaSource? fonte)
    {
        fonte = null;
        var link = ProcurarPlaca() ?? throw new InvalidOperationException("placa não conectada");
        using var fa = MediaFactory.MFCreateAttributes(2);
        fa.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
        fa.Set(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink, link);
        fonte = MediaFactory.MFCreateDeviceSource(fa);
        if (vez.Parar) throw new OperationCanceledException();

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
        Registro.Log($"captura aberta: {modo.Nome}");
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
        _tela.Apresentar(textura, dxgi.SubresourceIndex);
        Interlocked.Exchange(ref _ultimoQuadro, Environment.TickCount64);
        _aoQuadro?.Invoke((Agora100ns() - tempoAmostra) / 10_000.0);
        return true;
    }

    /// <summary>
    /// Relógio do PC (QPC) em 100 ns. O tempo da amostra da placa usa a mesma base:
    /// medido em 03/10/2026, 1º quadro com tempo 2248527147211 e relógio 2248527567580.
    /// </summary>
    static long Agora100ns() => (long)(System.Diagnostics.Stopwatch.GetTimestamp() * (10_000_000.0 / System.Diagnostics.Stopwatch.Frequency));

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
