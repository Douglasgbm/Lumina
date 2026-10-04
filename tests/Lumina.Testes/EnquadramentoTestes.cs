using Lumina.Nucleo;

namespace Lumina.Testes;

public class EnquadramentoTestes
{
    [Fact]
    public void Encaixar_area_16_9_ocupa_tudo()
    {
        Assert.Equal(new Retangulo(0, 0, 1920, 1080), Enquadramento.Encaixar(1920, 1080, 1280, 720));
    }

    [Fact]
    public void Encaixar_area_mais_alta_poe_barras_em_cima_e_embaixo()
    {
        Assert.Equal(new Retangulo(0, 60, 1920, 1080), Enquadramento.Encaixar(1920, 1200, 1280, 720));
        Assert.Equal(new Retangulo(0, 219, 1000, 562), Enquadramento.Encaixar(1000, 1000, 1280, 720));
    }

    [Fact]
    public void Encaixar_area_mais_larga_poe_barras_dos_lados()
    {
        Assert.Equal(new Retangulo(320, 0, 1920, 1080), Enquadramento.Encaixar(2560, 1080, 1920, 1080));
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-5, 720)]
    public void Encaixar_janela_minimizada_ou_sem_area_da_retangulo_vazio(int l, int a)
    {
        Assert.Equal(new Retangulo(0, 0, 0, 0), Enquadramento.Encaixar(l, a, 1280, 720));
    }

    static readonly Retangulo Principal = new(0, 0, 1920, 1080);
    static readonly Retangulo Segundo = new(1920, 0, 1920, 1080);

    [Fact]
    public void Visivel_no_segundo_monitor_fica_onde_estava()
    {
        var j = new Retangulo(2000, 100, 1280, 720);
        Assert.Equal(j, Enquadramento.GarantirVisivel(j, [Principal, Segundo], Principal));
    }

    [Fact]
    public void Segundo_monitor_desligado_traz_a_janela_para_o_principal()
    {
        var j = new Retangulo(2000, 100, 1280, 720);
        Assert.Equal(new Retangulo(320, 180, 1280, 720), Enquadramento.GarantirVisivel(j, [Principal], Principal));
    }

    [Fact]
    public void So_uma_tira_visivel_conta_como_perdida()
    {
        var j = new Retangulo(1880, 100, 1280, 720); // 40 px no principal
        Assert.Equal(new Retangulo(320, 180, 1280, 720), Enquadramento.GarantirVisivel(j, [Principal], Principal));
    }

    [Fact]
    public void Janela_maior_que_o_monitor_principal_encolhe()
    {
        var j = new Retangulo(5000, 0, 3840, 2160);
        Assert.Equal(Principal, Enquadramento.GarantirVisivel(j, [Principal], Principal));
    }
}
