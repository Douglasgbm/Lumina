using Lumina.Nucleo;

namespace Lumina.Testes;

public class MedidasTestes
{
    [Fact]
    public void Contador_da_60_com_quadros_a_cada_1_60_s()
    {
        var c = new ContadorQuadros();
        double? fps = null;
        for (int i = 0; i <= 60; i++) fps = c.Registrar(TimeSpan.FromSeconds(i / 60.0)) ?? fps;
        Assert.NotNull(fps);
        Assert.InRange(fps!.Value, 59.9, 60.1);
    }

    [Fact]
    public void Contador_nao_responde_antes_de_fechar_um_segundo()
    {
        var c = new ContadorQuadros();
        for (int i = 0; i < 30; i++) Assert.Null(c.Registrar(TimeSpan.FromSeconds(i / 60.0)));
    }

    [Fact]
    public void Contador_mede_30_quando_metade_dos_quadros_some()
    {
        var c = new ContadorQuadros();
        double? fps = null;
        for (int i = 0; i <= 60; i += 2) fps = c.Registrar(TimeSpan.FromSeconds(i / 60.0)) ?? fps;
        Assert.InRange(fps!.Value, 29.9, 30.1);
    }

    static float[] Tom(double hz, int taxa, double segundos, float amplitude = 0.5f) =>
        Enumerable.Range(0, (int)(taxa * segundos)).Select(i => amplitude * (float)Math.Sin(2 * Math.PI * hz * i / taxa)).ToArray();

    [Theory]
    [InlineData(1000)]
    [InlineData(1500)]
    [InlineData(440)]
    public void Frequencia_de_um_tom_puro(double hz)
    {
        Assert.InRange(Frequencia.Estimar(Tom(hz, 48000, 0.5), 48000)!.Value, hz - 2, hz + 2);
    }

    [Fact]
    public void Frequencia_lida_com_a_taxa_errada_sai_errada()
    {
        // É isso que a correção da MS2109 evita: 48 kHz lido como 96 kHz dobra o tom.
        Assert.InRange(Frequencia.Estimar(Tom(1000, 48000, 0.5), 96000)!.Value, 1998, 2002);
    }

    [Fact]
    public void Silencio_e_chiado_baixo_nao_viram_frequencia()
    {
        Assert.Null(Frequencia.Estimar(new float[24000], 48000));
        var rnd = new Random(1);
        var chiado = Enumerable.Range(0, 24000).Select(_ => (float)(rnd.NextDouble() - 0.5) * 0.004f).ToArray();
        Assert.Null(Frequencia.Estimar(chiado, 48000));
    }
}

public class ResumoAtrasoTestes
{
    [Fact]
    public void Resume_a_cada_N_amostras_com_mediana_p95_e_maximo()
    {
        var r = new ResumoAtraso(100);
        ResumoAtraso.Resumo? resumo = null;
        foreach (var ms in Enumerable.Range(1, 100).Reverse()) resumo = r.Registrar(ms) ?? resumo;
        Assert.Equal(new ResumoAtraso.Resumo(50, 95, 100), resumo);
    }

    [Fact]
    public void Nao_resume_antes_de_juntar_N_amostras()
    {
        var r = new ResumoAtraso(10);
        for (int i = 0; i < 9; i++) Assert.Null(r.Registrar(5));
        Assert.NotNull(r.Registrar(5));
    }

    [Fact]
    public void Depois_de_resumir_comeca_do_zero()
    {
        var r = new ResumoAtraso(2);
        Assert.Equal(new ResumoAtraso.Resumo(100, 100, 100), r.Registrar(100) ?? r.Registrar(100));
        r.Registrar(1);
        Assert.Equal(new ResumoAtraso.Resumo(1, 2, 2), r.Registrar(2));
    }
}
