using Lumina.Nucleo;

namespace Lumina.Testes;

public sealed class ConfiguracaoTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "lumina-testes-" + Guid.NewGuid().ToString("N"));
    string Caminho => Path.Combine(_pasta, "config.json");

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
    }

    [Fact]
    public void Sem_arquivo_devolve_o_padrao()
    {
        var c = new ArmazemConfiguracao(Caminho).Ler();
        Assert.Equal(new Configuracao(), c);
        Assert.Equal("720p60", c.Modo);
        Assert.False(c.TelaCheia);
        Assert.False(c.Maximizada);
    }

    [Fact]
    public void Salva_e_le_igual_criando_a_pasta()
    {
        var armazem = new ArmazemConfiguracao(Caminho);
        var c = new Configuracao { X = 2000, Y = 40, Largura = 1600, Altura = 900, Maximizada = true, TelaCheia = true, Modo = "1080p30", SaidaFixaId = "{0.0.0}.{x}", Volume = 0.5f, Mudo = true };
        armazem.Salvar(c);
        Assert.Equal(c, armazem.Ler());
        Assert.False(File.Exists(Caminho + ".tmp"));
    }

    [Theory]
    [InlineData("{{{ não é json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"Volume\": \"alto\"}")]
    [InlineData("[1,2,3]")]
    public void Arquivo_corrompido_volta_ao_padrao(string conteudo)
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, conteudo);
        Assert.Equal(new Configuracao(), new ArmazemConfiguracao(Caminho).Ler());
    }

    [Fact]
    public void Valores_absurdos_sao_corrigidos()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, "{\"Largura\": -5, \"Altura\": 999999, \"Volume\": 7, \"Modo\": \"4k\"}");
        var c = new ArmazemConfiguracao(Caminho).Ler();
        Assert.Equal(320, c.Largura);
        Assert.Equal(16384, c.Altura);
        Assert.Equal(1f, c.Volume);
        Assert.Equal("720p60", c.Modo);
    }

    [Fact]
    public void Campo_que_falta_fica_com_o_padrao()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, "{\"Mudo\": true}");
        var c = new ArmazemConfiguracao(Caminho).Ler();
        Assert.True(c.Mudo);
        Assert.Equal(1280, c.Largura);
        Assert.Equal(1f, c.Volume);
    }
}
