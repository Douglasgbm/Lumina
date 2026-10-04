using System.Text.RegularExpressions;

namespace Lumina.Nucleo;

/// <summary>
/// VID, PID e o "pai" da instância de um aparelho USB, tirados do link do vídeo ou do caminho de hardware do áudio.
/// Vídeo e áudio do mesmo aparelho têm o mesmo pai (medido em 03/10/2026):
/// <c>…usb#vid_534d&amp;pid_2109&amp;mi_00#7&amp;2cbab050&amp;0&amp;0000#…</c> × <c>{1}.USB\VID_534D&amp;PID_2109&amp;MI_02\7&amp;2CBAB050&amp;0&amp;0002</c>.
/// </summary>
public readonly record struct IdUsb(string Vid, string Pid, string Pai)
{
    static readonly Regex Padrao = new(
        @"usb[#\\]vid_([0-9a-f]{4})&pid_([0-9a-f]{4})(?:&mi_[0-9a-f]{2})?[#\\]([^#\\]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>null se não for um aparelho USB (placa PCIe, câmera virtual...).</summary>
    public static IdUsb? De(string texto)
    {
        var m = Padrao.Match(texto);
        if (!m.Success) return null;
        string instancia = m.Groups[3].Value;
        int ultimo = instancia.LastIndexOf('&');
        string pai = ultimo > 0 ? instancia[..ultimo] : instancia;
        return new(m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant(), pai.ToUpperInvariant());
    }

    /// <summary>Vídeo e áudio do mesmo aparelho físico.</summary>
    public bool MesmoAparelho(IdUsb outro) => MesmoModelo(outro) && Pai == outro.Pai;

    /// <summary>Mesmo modelo, talvez em outra porta USB.</summary>
    public bool MesmoModelo(IdUsb outro) => Vid == outro.Vid && Pid == outro.Pid;
}
