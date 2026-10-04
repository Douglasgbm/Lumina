namespace Lumina.Nucleo;

/// <summary>Um modo de imagem da placa. O nome ("720p60") é o que fica salvo na configuração.</summary>
public sealed record ModoVideo(int Largura, int Altura, int Fps)
{
    public static readonly ModoVideo Hd60 = new(1280, 720, 60);
    public static readonly ModoVideo FullHd30 = new(1920, 1080, 30);

    public string Nome => $"{Altura}p{Fps}";

    /// <summary>O modo fica à vista: o Douglas jogou 20 min em 1080p30 sem perceber (03/10/2026).</summary>
    public string Titulo => $"Lumina — {Nome}";
}
