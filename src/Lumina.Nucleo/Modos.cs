namespace Lumina.Nucleo;

/// <summary>Os modos que a placa oferece e que fazem sentido para jogar: 16:9, de 720p para cima, 30 ou 60 fps.</summary>
public static class Modos
{
    static readonly string[] Preferencia = ["MJPG", "NV12", "YUY2"];

    public static List<ModoVideo> Disponiveis(IEnumerable<FormatoNativo> formatos, string? link) =>
        formatos
            .Where(f => f.FpsDenominador > 0 && f.Altura >= 720 && (long)f.Largura * 9 == (long)f.Altura * 16)
            .Select(f => new ModoVideo(f.Largura, f.Altura, Fps(f)))
            .Where(m => m.Fps is 30 or 60)
            .Distinct()
            .Where(m => !DefeitoConhecido(link, m))
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

    /// <summary>MS2109: anuncia 1080p60 mas entrega 30 imagens diferentes por segundo (medido em 03/10/2026).</summary>
    static bool DefeitoConhecido(string? link, ModoVideo m) =>
        link is not null && Placa.EhMs2109(link) && m.Altura == 1080 && m.Fps == 60;

    static int Fps(FormatoNativo f) => (int)Math.Round((double)f.FpsNumerador / f.FpsDenominador);
}
