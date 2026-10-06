using Lumina.Nucleo;

namespace Lumina.Testes;

public class ModosTestes
{
    static FormatoNativo F(int i, string sub, int l, int a, int fps, int den = 1) => new(i, sub, l, a, fps * den, den);

    // Amostras reais (03/10/2026) do que cada câmera anuncia, incluindo o que o filtro tem que jogar fora.
    static readonly FormatoNativo[] FormatosMs2109 =
    [
        F(0, "NV12", 1920, 1080, 60), F(1, "MJPG", 1920, 1080, 60), F(3, "MJPG", 1920, 1080, 30),
        F(5, "MJPG", 1920, 1080, 25), F(11, "MJPG", 1600, 1200, 60), F(21, "MJPG", 1360, 768, 60),
        F(50, "NV12", 1280, 720, 60), F(51, "MJPG", 1280, 720, 60), F(53, "MJPG", 1280, 720, 50),
        F(55, "MJPG", 1280, 720, 30), F(101, "MJPG", 640, 480, 60), F(111, "YUY2", 1920, 1080, 5),
    ];

    static readonly FormatoNativo[] FormatosC270 =
    [
        F(0, "MJPG", 1280, 720, 30), F(1, "NV12", 1280, 720, 30), F(2, "YUY2", 1280, 720, 7, 2), // 7,5 fps
        F(3, "MJPG", 1184, 656, 30), F(4, "MJPG", 1280, 960, 30), F(5, "MJPG", 640, 360, 30),
    ];

    [Fact]
    public void MS2109_mostra_tambem_o_1080p60_a_pedido_do_Douglas()
    {
        // 05/10/2026: decisão dele (opção A) — liberar, e sem aviso, mesmo sabendo que a placa entrega 30 quadros bons.
        var modos = Modos.Disponiveis(FormatosMs2109, PlacaTestes.Ms2109.Link);
        Assert.Equal(["720p30", "720p60", "1080p30", "1080p60"], modos.Select(m => m.Nome));
    }

    [Fact]
    public void Outra_placa_com_1080p60_mostra_o_1080p60()
    {
        var modos = Modos.Disponiveis(FormatosMs2109, @"\\?\usb#vid_345f&pid_2130&mi_00#8&1&0&0000#{e5323777}\global");
        Assert.Contains(modos, m => m.Nome == "1080p60");
    }

    [Fact]
    public void C270_so_tem_720p30()
    {
        Assert.Equal(["720p30"], Modos.Disponiveis(FormatosC270, PlacaTestes.C270.Link).Select(m => m.Nome));
    }

    [Fact]
    public void Fps_fracionado_arredonda()
    {
        var modos = Modos.Disponiveis([new FormatoNativo(0, "NV12", 1920, 1080, 60000, 1001)], null); // 59,94
        Assert.Equal(["1080p60"], modos.Select(m => m.Nome));
    }

    [Fact]
    public void Escolher_usa_o_salvo_senao_720p60_senao_o_primeiro()
    {
        var ms = Modos.Disponiveis(FormatosMs2109, PlacaTestes.Ms2109.Link);
        Assert.Equal("1080p30", Modos.Escolher(ms, "1080p30")!.Nome);
        Assert.Equal("720p60", Modos.Escolher(ms, "4k")!.Nome);
        Assert.Equal("1080p60", Modos.Escolher(ms, "1080p60")!.Nome); // liberado na MS2109 (05/10/2026)
        var c270 = Modos.Disponiveis(FormatosC270, PlacaTestes.C270.Link);
        Assert.Equal("720p30", Modos.Escolher(c270, "720p60")!.Nome);
        Assert.Null(Modos.Escolher([], "720p60"));
    }

    [Fact]
    public void Formato_prefere_MJPG_depois_NV12_depois_YUY2()
    {
        Assert.Equal(51, Modos.FormatoPara(FormatosMs2109, ModoVideo.Hd60));
        Assert.Equal(0, Modos.FormatoPara([F(0, "NV12", 1280, 720, 60), F(1, "YUY2", 1280, 720, 60)], ModoVideo.Hd60));
        Assert.Equal(7, Modos.FormatoPara([F(7, "outro", 1280, 720, 60)], ModoVideo.Hd60));
        Assert.Null(Modos.FormatoPara(FormatosC270, ModoVideo.Hd60));
    }

    [Fact]
    public void Nome_e_titulo_do_modo()
    {
        Assert.Equal("720p60", ModoVideo.Hd60.Nome);
        Assert.Equal("Lumina — 1080p30", ModoVideo.FullHd30.Titulo);
        Assert.Equal(ModoVideo.Hd60, new ModoVideo(1280, 720, 60));
    }
}
