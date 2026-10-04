using Lumina.Nucleo;

namespace Lumina.Testes;

public class PlacaTestes
{
    // Links e IDs copiados da máquina do Douglas em 03/10/2026.
    internal static readonly DispositivoVideo C270 = new("Logi C270 HD WebCam",
        @"\\?\usb#vid_046d&pid_0825&mi_00#7&33b9e16e&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global");
    internal static readonly DispositivoVideo Ms2109 = new("USB Video",
        @"\\?\usb#vid_534d&pid_2109&mi_00#7&2cbab050&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global");
    internal static readonly DispositivoAudio MicC270 = new("{0.0.1}.{a}", "Microfone (Logi C270 HD WebCam)", @"{1}.USB\VID_046D&PID_0825&MI_02\7&33B9E16E&0&0002");
    internal static readonly DispositivoAudio Realtek = new("{0.0.1}.{b}", "Microfone (Realtek(R) Audio)", @"{1}.HDAUDIO\FUNC_01&VEN_10EC&DEV_0897&SUBSYS_1458A194&REV_1004\5&E50FC96&0&0001");
    internal static readonly DispositivoAudio AudioMs2109 = new("{0.0.1}.{08d9fed2}", "Interface de áudio digital (USB Digital Audio)", @"{1}.USB\VID_534D&PID_2109&MI_02\7&2CBAB050&0&0002");

    [Fact]
    public void IdUsb_le_o_link_do_video_e_o_hardware_do_audio_do_mesmo_aparelho()
    {
        var video = IdUsb.De(Ms2109.Link);
        var audio = IdUsb.De(AudioMs2109.Hardware);
        Assert.Equal(new IdUsb("534D", "2109", "7&2CBAB050&0"), video);
        Assert.True(video!.Value.MesmoAparelho(audio!.Value));
    }

    [Fact]
    public void IdUsb_nao_confunde_aparelhos_diferentes()
    {
        Assert.False(IdUsb.De(Ms2109.Link)!.Value.MesmoAparelho(IdUsb.De(MicC270.Hardware)!.Value));
    }

    [Fact]
    public void IdUsb_de_algo_que_nao_e_USB_e_nulo()
    {
        Assert.Null(IdUsb.De(Realtek.Hardware));
        Assert.Null(IdUsb.De(@"\\?\root#camera#0000#{e5323777}\global")); // câmera virtual
    }

    [Fact]
    public void Nada_salvo_usa_a_MS2109_mesmo_com_a_webcam_antes_na_lista()
    {
        Assert.Equal(new ResultadoPlaca(Ms2109, null), Placa.Resolver([C270, Ms2109], null, null));
    }

    [Fact]
    public void Nada_salvo_e_sem_MS2109_nunca_abre_a_webcam_sozinho()
    {
        Assert.Equal(new ResultadoPlaca(null, MotivoSemPlaca.NenhumaEscolhida), Placa.Resolver([C270], null, null));
    }

    [Fact]
    public void A_escolhida_e_usada_mesmo_com_a_MS2109_conectada()
    {
        Assert.Equal(new ResultadoPlaca(C270, null), Placa.Resolver([C270, Ms2109], C270.Link, C270.Nome));
    }

    [Fact]
    public void A_escolhida_em_outra_porta_USB_e_reconhecida_pelo_nome_e_modelo()
    {
        var outraPorta = Ms2109 with { Link = @"\\?\usb#vid_534d&pid_2109&mi_00#7&99aa11bb&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global" };
        Assert.Equal(new ResultadoPlaca(outraPorta, null), Placa.Resolver([C270, outraPorta], Ms2109.Link, Ms2109.Nome));
    }

    [Fact]
    public void A_escolhida_desconectada_nao_troca_por_outra_nem_pela_MS2109()
    {
        Assert.Equal(new ResultadoPlaca(null, MotivoSemPlaca.EscolhidaAusente), Placa.Resolver([Ms2109], C270.Link, C270.Nome));
    }

    [Fact]
    public void Outra_placa_generica_de_mesmo_nome_nao_e_a_MS2109()
    {
        var outra = new DispositivoVideo("USB Video", @"\\?\usb#vid_345f&pid_2130&mi_00#8&1&0&0000#{e5323777}\global");
        Assert.False(Placa.EhMs2109(outra.Link));
        Assert.Equal(MotivoSemPlaca.NenhumaEscolhida, Placa.Resolver([outra], null, null).Motivo);
    }
}
