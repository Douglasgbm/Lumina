namespace Lumina.Nucleo;

public sealed record DispositivoVideo(string Nome, string Link);

public sealed record DispositivoAudio(string Id, string Nome, string Hardware);

public sealed record FormatoNativo(int Indice, string Subtipo, int Largura, int Altura, int FpsNumerador, int FpsDenominador);

public enum MotivoSemPlaca { NenhumaEscolhida, EscolhidaAusente }

public sealed record ResultadoPlaca(DispositivoVideo? Placa, MotivoSemPlaca? Motivo);

/// <summary>Qual câmera do Windows é a placa de captura.</summary>
public static class Placa
{
    /// <summary>MacroSilicon MS2109, a placa do Douglas (medido em 03/10/2026).</summary>
    public const string IdMs2109 = "VID_534D&PID_2109";

    public static bool EhMs2109(string linkOuHardware) =>
        linkOuHardware.Contains(IdMs2109, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 1º a escolha salva pelo link; 2º a mesma placa em outra porta USB (mesmo nome e modelo);
    /// nada salvo → a MS2109 se estiver conectada. Nunca troca a escolhida por outra câmera.
    /// </summary>
    public static ResultadoPlaca Resolver(IReadOnlyList<DispositivoVideo> lista, string? linkSalvo, string? nomeSalvo)
    {
        if (linkSalvo is not null)
        {
            var exata = lista.FirstOrDefault(d => string.Equals(d.Link, linkSalvo, StringComparison.OrdinalIgnoreCase));
            if (exata is not null) return new(exata, null);
            var idSalvo = IdUsb.De(linkSalvo);
            var candidatas = lista.Where(d => d.Nome == nomeSalvo
                && idSalvo is { } s && IdUsb.De(d.Link) is { } i && i.MesmoModelo(s)).ToList();
            // Só é "a mesma em outra porta" se for a única do modelo: com duas iguais (dois consoles),
            // pegar uma seria trocar a escolhida pela outra em silêncio (revisão final, 05/10/2026).
            return candidatas.Count == 1 ? new(candidatas[0], null) : new(null, MotivoSemPlaca.EscolhidaAusente);
        }
        var ms2109 = lista.FirstOrDefault(d => EhMs2109(d.Link));
        return ms2109 is not null ? new(ms2109, null) : new(null, MotivoSemPlaca.NenhumaEscolhida);
    }
}
