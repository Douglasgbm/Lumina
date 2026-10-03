namespace Lumina.Nucleo;

public enum ProblemaCaptura { Nenhum, PlacaAusente, PlacaOcupada, AcessoNegado, Outro }

/// <summary>O que mostrar no lugar da imagem: uma frase que diga o que houve, em vez de tela branca muda.</summary>
public static class Problemas
{
    public static ProblemaCaptura DeHResult(int hresult) => unchecked((uint)hresult) switch
    {
        0x80070005 => ProblemaCaptura.AcessoNegado,  // E_ACCESSDENIED
        0xC00D3704 => ProblemaCaptura.PlacaOcupada,  // MF_E_HW_MFT_FAILED_START_STREAMING
        0xC00D3EA2 => ProblemaCaptura.PlacaAusente,  // MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED
        _ => ProblemaCaptura.Outro,
    };

    public static string Mensagem(ProblemaCaptura p) => p switch
    {
        // Medido em 03/10/2026: o Auto-Sandbox do Norton 360 isola cada versão nova na 1ª abertura.
        ProblemaCaptura.AcessoNegado =>
            "Acesso à câmera negado.\nAlguns antivírus (ex.: Norton) isolam um programa novo na primeira vez: feche e abra de novo.\nSe continuar, veja Configurações > Privacidade > Câmera no Windows.",
        ProblemaCaptura.PlacaOcupada =>
            "A placa está em uso por outro programa (OBS, Discord...).\nFeche o outro programa: a imagem volta sozinha.",
        ProblemaCaptura.PlacaAusente =>
            "Placa de captura não encontrada.\nConfira o cabo USB: a imagem volta sozinha.",
        _ => "sem sinal",
    };
}
