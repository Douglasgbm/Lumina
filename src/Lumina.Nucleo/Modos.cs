namespace Lumina.Nucleo;

/// <summary>
/// Os modos que a placa oferece e que fazem sentido para jogar: 16:9, de 720p para cima, 30 ou 60 fps.
/// O 1080p60 da MS2109 aparece sem aviso (decisão do Douglas, 05/10/2026), embora ela entregue 30 quadros bons (medido).
/// </summary>
public static class Modos
{
    static readonly string[] Preferencia = ["MJPG", "NV12", "YUY2"];

    public static List<ModoVideo> Disponiveis(IEnumerable<FormatoNativo> formatos, string? link) =>
        formatos
            .Where(f => f.FpsDenominador > 0 && f.Altura >= 720 && (long)f.Largura * 9 == (long)f.Altura * 16)
            .Select(f => new ModoVideo(f.Largura, f.Altura, Fps(f)))
            .Where(m => m.Fps is 30 or 60)
            .Distinct()
            .OrderBy(m => m.Altura).ThenBy(m => m.Fps)
            .ToList();

    /// <summary>O salvo se a placa tiver; senão 720p60; senão o primeiro; null se a placa não tem nenhum modo útil.</summary>
    public static ModoVideo? Escolher(IReadOnlyList<ModoVideo> modos, string? nomeSalvo) =>
        modos.FirstOrDefault(m => m.Nome == nomeSalvo)
        ?? modos.FirstOrDefault(m => m == ModoVideo.Hd60)
        ?? modos.FirstOrDefault();

    /// <summary>Índice do formato nativo do modo, preferindo MJPG (comprimido, cabe no USB 2.0), depois NV12, YUY2.</summary>
    public static int? FormatoPara(IEnumerable<FormatoNativo> formatos, ModoVideo modo)
    {
        var candidatos = formatos
            .Where(f => f.FpsDenominador > 0 && f.Largura == modo.Largura && f.Altura == modo.Altura && Fps(f) == modo.Fps)
            .ToList();
        foreach (var subtipo in Preferencia)
            if (candidatos.FirstOrDefault(f => f.Subtipo == subtipo) is { } f) return f.Indice;
        return candidatos.FirstOrDefault()?.Indice;
    }

    static int Fps(FormatoNativo f) => (int)Math.Round((double)f.FpsNumerador / f.FpsDenominador);
}
