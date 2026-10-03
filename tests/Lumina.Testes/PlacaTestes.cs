using Lumina.Nucleo;

namespace Lumina.Testes;

public class PlacaTestes
{
    // Links e IDs copiados da máquina do Douglas em 03/10/2026.
    static readonly DispositivoVideo C270 = new("Logi C270 HD WebCam",
        @"\\?\usb#vid_046d&pid_0825&mi_00#7&33b9e16e&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global");
    static readonly DispositivoVideo Ms2109 = new("USB Video",
        @"\\?\usb#vid_534d&pid_2109&mi_00#7&2cbab050&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global");

    [Fact]
    public void Video_escolhe_a_placa_mesmo_com_a_webcam_antes_na_lista()
    {
        Assert.Equal(Ms2109, Placa.EscolherVideo([C270, Ms2109]));
    }

    [Fact]
    public void Video_nunca_escolhe_a_webcam_quando_a_placa_nao_esta_conectada()
    {
        Assert.Null(Placa.EscolherVideo([C270]));
    }

    [Fact]
    public void Video_nao_se_engana_com_outra_placa_generica_de_mesmo_nome()
    {
        var outra = new DispositivoVideo("USB Video", @"\\?\usb#vid_345f&pid_2130&mi_00#8&1&0&0000#{e5323777}\global");
        Assert.Null(Placa.EscolherVideo([outra]));
    }

    [Fact]
    public void Audio_escolhe_pelo_hardware_e_nao_pelo_nome()
    {
        var mic = new DispositivoAudio("{0.0.1}.{a}", "Microfone (Logi C270 HD WebCam)", @"{1}.USB\VID_046D&PID_0825&MI_02\7&33B9E16E&0&0002");
        var realtek = new DispositivoAudio("{0.0.1}.{b}", "Microfone (Realtek(R) Audio)", @"{1}.HDAUDIO\FUNC_01&VEN_10EC&DEV_0897");
        var placa = new DispositivoAudio("{0.0.1}.{08d9fed2}", "Interface de áudio digital (USB Digital Audio)", @"{1}.USB\VID_534D&PID_2109&MI_02\7&2CBAB050&0&0002");
        Assert.Equal(placa, Placa.EscolherAudio([mic, realtek, placa]));
        Assert.Null(Placa.EscolherAudio([mic, realtek]));
    }

    [Fact]
    public void Formato_nativo_prefere_MJPG_exato_e_ignora_NV12_do_mesmo_tamanho()
    {
        var formatos = new[]
        {
            new FormatoNativo(50, "NV12", 1280, 720, 60, 1),
            new FormatoNativo(51, "MJPG", 1280, 720, 60, 1),
            new FormatoNativo(55, "MJPG", 1280, 720, 30, 1),
            new FormatoNativo(3, "MJPG", 1920, 1080, 30, 1),
        };
        Assert.Equal(51, Placa.EscolherFormatoNativo(formatos, ModoVideo.Hd60));
        Assert.Equal(3, Placa.EscolherFormatoNativo(formatos, ModoVideo.FullHd30));
    }

    [Fact]
    public void Formato_nativo_aceita_fps_em_fracao_equivalente()
    {
        Assert.Equal(7, Placa.EscolherFormatoNativo([new FormatoNativo(7, "MJPG", 1280, 720, 120, 2)], ModoVideo.Hd60));
    }

    [Fact]
    public void Formato_nativo_null_quando_a_placa_nao_oferece_o_modo()
    {
        Assert.Null(Placa.EscolherFormatoNativo([new FormatoNativo(0, "MJPG", 640, 480, 60, 1)], ModoVideo.Hd60));
        Assert.Null(Placa.EscolherFormatoNativo([new FormatoNativo(0, "MJPG", 1280, 720, 60, 0)], ModoVideo.Hd60));
    }
}
