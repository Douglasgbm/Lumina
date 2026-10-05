using Lumina.Nucleo;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Lumina;

/// <summary>
/// Captura o som da placa, corrige o formato anunciado e toca na saída escolhida.
/// Tudo aqui roda na thread da janela; avisos do Windows chegam por _ui.Post.
/// </summary>
sealed class MotorAudio : IMMNotificationClient, IDisposable
{
    static readonly Guid SubtipoFloat = new("00000003-0000-0010-8000-00aa00389b71");
    // Guarda o caminho do hardware pai, ex.: {1}.USB\VID_534D&PID_2109&MI_02\... (medido em 03/10/2026).
    static readonly PropertyKey ChaveHardware = new(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 2);

    readonly MMDeviceEnumerator _enumerador = new();
    readonly SynchronizationContext _ui;
    readonly Action<float[], float[]>? _aoBloco;
    WasapiCapture? _captura;
    WasapiOut? _saida;
    BufferedWaveProvider? _fila;
    VolumeSampleProvider? _volume;
    string? _saidaFixaId;
    float _nivel = 1f;
    bool _mudo;
    string? _ultimoAviso;

    /// <summary>A placa de vídeo em uso: a entrada automática é a do mesmo aparelho USB.</summary>
    public DispositivoVideo? Placa { get; set; }

    /// <summary>null = automático; EntradaSom.Nenhuma = sem som; ou o id de uma entrada.</summary>
    public string? EntradaEscolhida { get; set; }

    /// <param name="aoBloco">Só no diagnóstico: recebe os canais esquerdo e direito já corrigidos.</param>
    public MotorAudio(SynchronizationContext ui, Action<float[], float[]>? aoBloco)
    {
        _ui = ui;
        _aoBloco = aoBloco;
        _enumerador.RegisterEndpointNotificationCallback(this);
    }

    /// <summary>
    /// Entrada E saída de pé. Se uma falhar (placa sumiu, saída em uso exclusivo, permissão), fica falso
    /// e a janela tenta Iniciar de novo; antes ficava "ativo" e mudo para sempre (revisão final).
    /// </summary>
    public bool Ativo => _captura is not null && _saida is not null;

    public float Volume
    {
        get => _nivel;
        set { _nivel = Math.Clamp(value, 0f, 1f); AplicarVolume(); }
    }

    public bool Mudo
    {
        get => _mudo;
        set { _mudo = value; AplicarVolume(); }
    }

    /// <summary>null = segue a saída padrão do Windows.</summary>
    public string? SaidaFixaId
    {
        get => _saidaFixaId;
        set { _saidaFixaId = value; if (Ativo) ReiniciarSaida(); }
    }

    public List<DispositivoAudio> Entradas() =>
        _enumerador.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new DispositivoAudio(d.ID, d.FriendlyName, Hardware(d))).ToList();

    public List<(string Id, string Nome)> Saidas() =>
        _enumerador.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Select(d => (d.ID, d.FriendlyName)).ToList();

    public void Iniciar()
    {
        Parar();
        var escolhido = EntradaSom.Resolver(EntradaEscolhida, Entradas(), Placa);
        if (escolhido is null)
        {
            Avisar(EntradaEscolhida == EntradaSom.Nenhuma ? "áudio: sem som (escolhido no menu)" : "áudio: nenhuma entrada de som da placa encontrada");
            return;
        }
        var dispositivo = _enumerador.GetDevice(escolhido.Id);

        var captura = new WasapiCapture(dispositivo, true, 10);
        var bruto = captura.WaveFormat;
        bool flutuante = bruto.Encoding == WaveFormatEncoding.IeeeFloat
            || (bruto is WaveFormatExtensible x && x.SubFormat == SubtipoFloat);
        var anunciado = new FormatoPcm(bruto.SampleRate, bruto.Channels, bruto.BitsPerSample, flutuante);
        // O defeito do 96 kHz mono é da MS2109; outras placas usam o formato como o Windows informa.
        var c = Nucleo.Placa.EhMs2109(escolhido.Hardware) ? CorrecaoMs2109.Corrigir(anunciado) : anunciado;
        var formato = flutuante ? WaveFormat.CreateIeeeFloatWaveFormat(c.Taxa, c.Canais) : new WaveFormat(c.Taxa, c.Bits, c.Canais);
        _ultimoAviso = null;
        Registro.Log($"áudio: {escolhido.Nome}: anunciado {bruto} → usado {formato}");

        var fila = new BufferedWaveProvider(formato)
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };
        _fila = fila;
        _volume = new VolumeSampleProvider(fila.ToSampleProvider());
        AplicarVolume();
        captura.DataAvailable += (_, e) =>
        {
            if (FilaAudio.DeveLimpar(fila.BufferedDuration)) fila.ClearBuffer();
            fila.AddSamples(e.Buffer, 0, e.BytesRecorded);
            if (_aoBloco is not null && flutuante && formato.Channels == 2) Separar(e.Buffer, e.BytesRecorded);
        };
        captura.RecordingStopped += (_, e) =>
        {
            // Placa desconectada: para tudo; a janela tenta Iniciar de novo.
            if (e.Exception is null) return;
            Registro.Erro("áudio.captura", e.Exception);
            _ui.Post(_ => { if (ReferenceEquals(_captura, captura)) Parar(); }, null);
        };
        _captura = captura;
        try
        {
            captura.StartRecording();
            IniciarSaida();
        }
        catch
        {
            Parar();
            throw;
        }
    }

    public void Parar()
    {
        _saida?.Dispose();
        _saida = null;
        var captura = _captura;
        _captura = null;
        if (captura is not null)
        {
            try { captura.StopRecording(); } catch (Exception e) { Registro.Erro("áudio.parar", e); }
            captura.Dispose();
        }
        _fila = null;
        _volume = null;
    }

    public void Dispose()
    {
        _enumerador.UnregisterEndpointNotificationCallback(this);
        Parar();
        _enumerador.Dispose();
    }

    /// <summary>O vigia tenta de novo a cada 2 s: sem isso o mesmo aviso encheria o log.</summary>
    void Avisar(string linha)
    {
        if (linha == _ultimoAviso) return;
        _ultimoAviso = linha;
        Registro.Log(linha);
    }

    void AplicarVolume()
    {
        if (_volume is not null) _volume.Volume = _mudo ? 0f : _nivel;
    }

    void IniciarSaida()
    {
        if (_volume is null) return;
        var ids = Saidas().Select(s => s.Id).ToList();
        string padrao = _enumerador.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        var alvo = _enumerador.GetDevice(SaidaAudio.Resolver(_saidaFixaId, ids, padrao));
        var saida = new WasapiOut(alvo, AudioClientShareMode.Shared, true, 20);
        saida.PlaybackStopped += (_, e) =>
        {
            // Saída removida (headset desligado): volta a tocar onde der.
            if (e.Exception is null) return;
            Registro.Erro("áudio.saída", e.Exception);
            _ui.Post(_ => { if (ReferenceEquals(_saida, saida)) ReiniciarSaida(); }, null);
        };
        saida.Init(_volume);
        saida.Play();
        _saida = saida;
        Registro.Log($"áudio: tocando em {alvo.FriendlyName}");
    }

    void ReiniciarSaida()
    {
        _saida?.Dispose();
        _saida = null;
        try { IniciarSaida(); }
        catch (Exception e) { Registro.Erro("áudio.saída", e); }
    }

    void Separar(byte[] buffer, int bytes)
    {
        int quadros = bytes / 8;
        var esquerdo = new float[quadros];
        var direito = new float[quadros];
        for (int i = 0; i < quadros; i++)
        {
            esquerdo[i] = BitConverter.ToSingle(buffer, i * 8);
            direito[i] = BitConverter.ToSingle(buffer, i * 8 + 4);
        }
        _aoBloco!(esquerdo, direito);
    }

    static string Hardware(MMDevice d)
    {
        try { return d.Properties.Contains(ChaveHardware) ? d.Properties[ChaveHardware].Value?.ToString() ?? "" : ""; }
        catch (Exception) { return ""; }
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia)
            _ui.Post(_ => { if (Ativo && _saidaFixaId is null) ReiniciarSaida(); }, null);
    }

    /// <summary>Headset fixado (saída) ou entrada escolhida religados: volta a usar eles sozinho.</summary>
    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        if (newState != DeviceState.Active) return;
        _ui.Post(_ =>
        {
            if (deviceId == _saidaFixaId && Ativo) ReiniciarSaida();
            else if (deviceId == EntradaEscolhida && Placa is not null) // sem placa, sem som
            {
                try { Iniciar(); }
                catch (Exception e) { Registro.Erro("áudio.entrada", e); }
            }
        }, null);
    }
    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
}
