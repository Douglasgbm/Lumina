using Lumina.Nucleo;

namespace Lumina.Testes;

public class RegrasAudioTestes
{
    [Fact]
    public void Corrige_o_anuncio_da_MS2109_float()
    {
        // Formato que o WASAPI entregou na máquina do Douglas em 03/10/2026.
        Assert.Equal(new FormatoPcm(48000, 2, 32, true), CorrecaoMs2109.Corrigir(new FormatoPcm(96000, 1, 32, true)));
    }

    [Fact]
    public void Corrige_o_anuncio_da_MS2109_16_bits()
    {
        Assert.Equal(new FormatoPcm(48000, 2, 16, false), CorrecaoMs2109.Corrigir(new FormatoPcm(96000, 1, 16, false)));
    }

    [Theory]
    [InlineData(48000, 2)]
    [InlineData(96000, 2)]
    [InlineData(44100, 1)]
    public void Formato_que_ja_e_verdadeiro_nao_muda(int taxa, int canais)
    {
        var f = new FormatoPcm(taxa, canais, 32, true);
        Assert.Equal(f, CorrecaoMs2109.Corrigir(f));
    }

    [Fact]
    public void Fila_so_limpa_depois_do_limite()
    {
        Assert.False(FilaAudio.DeveLimpar(TimeSpan.FromMilliseconds(60)));
        Assert.True(FilaAudio.DeveLimpar(TimeSpan.FromMilliseconds(61)));
    }

    [Fact]
    public void Saida_fixada_que_existe_vence_o_padrao()
    {
        Assert.Equal("headset", SaidaAudio.Resolver("headset", ["headset", "caixa"], "caixa"));
    }

    [Fact]
    public void Saida_fixada_que_sumiu_cai_no_padrao()
    {
        Assert.Equal("caixa", SaidaAudio.Resolver("headset", ["caixa"], "caixa"));
    }

    [Fact]
    public void Sem_saida_fixada_segue_o_padrao()
    {
        Assert.Equal("caixa", SaidaAudio.Resolver(null, ["headset", "caixa"], "caixa"));
    }
}
