using Lumina.Nucleo;

namespace Lumina.Testes;

public class ProblemasTestes
{
    // Códigos vistos no lumina.log do Douglas em 03/10/2026.
    [Theory]
    [InlineData(unchecked((int)0x80070005), ProblemaCaptura.AcessoNegado)]  // E_ACCESSDENIED
    [InlineData(unchecked((int)0xC00D3704), ProblemaCaptura.PlacaOcupada)]  // MF_E_HW_MFT_FAILED_START_STREAMING (ffplay segurando, 11:41)
    [InlineData(unchecked((int)0xC00D3EA2), ProblemaCaptura.PlacaAusente)]  // MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED (USB tirado, 12:05)
    [InlineData(unchecked((int)0x80004005), ProblemaCaptura.Outro)]         // E_FAIL qualquer
    public void Codigo_do_Windows_vira_um_problema_conhecido(int hresult, ProblemaCaptura esperado)
    {
        Assert.Equal(esperado, Problemas.DeHResult(hresult));
    }

    [Fact]
    public void Acesso_negado_aponta_o_antivirus_e_a_privacidade_do_Windows()
    {
        // Causa medida em 03/10/2026: Auto-Sandbox do Norton 360 isola cada versão nova na 1ª abertura.
        var m = Problemas.Mensagem(ProblemaCaptura.AcessoNegado);
        Assert.Contains("negado", m);
        Assert.Contains("antivírus", m);
        Assert.Contains("abra de novo", m);
        Assert.Contains("Privacidade", m);
    }

    [Fact]
    public void Placa_ocupada_sugere_fechar_o_outro_programa()
    {
        Assert.Contains("outro programa", Problemas.Mensagem(ProblemaCaptura.PlacaOcupada));
    }

    [Fact]
    public void Placa_ausente_sugere_conferir_o_USB()
    {
        Assert.Contains("USB", Problemas.Mensagem(ProblemaCaptura.PlacaAusente));
    }

    [Theory]
    [InlineData(ProblemaCaptura.Nenhum)]
    [InlineData(ProblemaCaptura.Outro)]
    public void Sem_problema_identificado_continua_sem_sinal(ProblemaCaptura p)
    {
        Assert.Equal("sem sinal", Problemas.Mensagem(p));
    }

    [Fact]
    public void Sem_placa_escolhida_ensina_onde_escolher()
    {
        Assert.Equal(ProblemaCaptura.NenhumaPlacaEscolhida, Problemas.De(MotivoSemPlaca.NenhumaEscolhida));
        Assert.Contains("Placa", Problemas.Mensagem(ProblemaCaptura.NenhumaPlacaEscolhida));
    }

    [Fact]
    public void Placa_escolhida_ausente_diz_o_nome_dela()
    {
        Assert.Equal(ProblemaCaptura.PlacaEscolhidaAusente, Problemas.De(MotivoSemPlaca.EscolhidaAusente));
        Assert.Contains("\"Logi C270 HD WebCam\" não está conectada", Problemas.Mensagem(ProblemaCaptura.PlacaEscolhidaAusente, "Logi C270 HD WebCam"));
    }

    [Fact]
    public void Camera_sem_modo_util_pede_outra_placa()
    {
        Assert.Contains("720p", Problemas.Mensagem(ProblemaCaptura.SemModoUtil));
    }
}
