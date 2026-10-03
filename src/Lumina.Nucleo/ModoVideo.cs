namespace Lumina.Nucleo;

/// <summary>Os dois modos que o botão alterna. Começa em 720p60 (decisão do Douglas, 03/10/2026).</summary>
public sealed record ModoVideo(string Nome, int Largura, int Altura, int Fps)
{
    public static readonly ModoVideo Hd60 = new("720p60", 1280, 720, 60);
    public static readonly ModoVideo FullHd30 = new("1080p30", 1920, 1080, 30);

    /// <summary>Nome desconhecido ou nulo cai no padrão.</summary>
    public static ModoVideo PorNome(string? nome) => nome == FullHd30.Nome ? FullHd30 : Hd60;

    public ModoVideo Alternar() => this == Hd60 ? FullHd30 : Hd60;
}
