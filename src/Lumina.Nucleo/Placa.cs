namespace Lumina.Nucleo;

public sealed record DispositivoVideo(string Nome, string Link);

public sealed record DispositivoAudio(string Id, string Nome, string Hardware);

public sealed record FormatoNativo(int Indice, string Subtipo, int Largura, int Altura, int FpsNumerador, int FpsDenominador);

/// <summary>Como reconhecer a placa de captura entre os dispositivos do Windows.</summary>
public static class Placa
{
    /// <summary>MacroSilicon MS2109 (medido em 03/10/2026). Vídeo e áudio carregam o mesmo ID.</summary>
    public const string IdHardware = "VID_534D&PID_2109";

    public static DispositivoVideo? EscolherVideo(IEnumerable<DispositivoVideo> lista) =>
        lista.FirstOrDefault(d => d.Link.Contains(IdHardware, StringComparison.OrdinalIgnoreCase));

    public static DispositivoAudio? EscolherAudio(IEnumerable<DispositivoAudio> lista) =>
        lista.FirstOrDefault(d => d.Hardware.Contains(IdHardware, StringComparison.OrdinalIgnoreCase));

    /// <summary>Índice do formato nativo MJPG com tamanho e fps exatos do modo; null se a placa não oferece.</summary>
    public static int? EscolherFormatoNativo(IEnumerable<FormatoNativo> formatos, ModoVideo modo) =>
        formatos.FirstOrDefault(f => f.Subtipo == "MJPG"
            && f.Largura == modo.Largura && f.Altura == modo.Altura
            && f.FpsDenominador > 0 && f.FpsNumerador == modo.Fps * f.FpsDenominador)?.Indice;
}
