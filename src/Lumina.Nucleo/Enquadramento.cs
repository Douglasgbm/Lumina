namespace Lumina.Nucleo;

public readonly record struct Retangulo(int X, int Y, int Largura, int Altura);

public static class Enquadramento
{
    /// <summary>Maior retângulo com a proporção do vídeo que cabe na área, centralizado (barras pretas no resto).</summary>
    public static Retangulo Encaixar(int larguraArea, int alturaArea, int larguraVideo, int alturaVideo)
    {
        if (larguraArea <= 0 || alturaArea <= 0 || larguraVideo <= 0 || alturaVideo <= 0) return new(0, 0, 0, 0);
        long l = larguraArea;
        long a = (long)larguraArea * alturaVideo / larguraVideo;
        if (a > alturaArea)
        {
            a = alturaArea;
            l = (long)alturaArea * larguraVideo / alturaVideo;
        }
        return new((int)((larguraArea - l) / 2), (int)((alturaArea - a) / 2), (int)l, (int)a);
    }

    /// <summary>
    /// Mantém a janela onde estava se a barra de título aparece em algum monitor (pelo menos 100 px de largura
    /// e 20 de altura da faixa de cima — senão não dá para arrastar); senão a centraliza no monitor principal,
    /// encolhendo se for maior que ele. Contas em long: X/Y gigantes do config davam a volta no int.
    /// </summary>
    public static Retangulo GarantirVisivel(Retangulo janela, IReadOnlyList<Retangulo> telas, Retangulo principal)
    {
        const int BarraDeTitulo = 30;
        long jx = janela.X, jy = janela.Y, barra = Math.Min(BarraDeTitulo, janela.Altura);
        foreach (var t in telas)
        {
            long l = Math.Min(jx + janela.Largura, (long)t.X + t.Largura) - Math.Max(jx, t.X);
            long a = Math.Min(jy + barra, (long)t.Y + t.Altura) - Math.Max(jy, t.Y);
            if (l >= 100 && a >= Math.Min(20, barra)) return janela;
        }
        int largura = Math.Min(janela.Largura, principal.Largura);
        int altura = Math.Min(janela.Altura, principal.Altura);
        return new(principal.X + (principal.Largura - largura) / 2, principal.Y + (principal.Altura - altura) / 2, largura, altura);
    }
}
