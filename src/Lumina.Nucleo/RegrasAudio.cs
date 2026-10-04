namespace Lumina.Nucleo;

public readonly record struct FormatoPcm(int Taxa, int Canais, int Bits, bool PontoFlutuante);

public static class CorrecaoMs2109
{
    /// <summary>
    /// A MS2109 se anuncia como 96 kHz mono, mas manda 48 kHz estéreo intercalado
    /// (anúncio medido no registro do Windows em 03/10/2026). Os bytes não mudam, só a leitura deles.
    /// </summary>
    public static FormatoPcm Corrigir(FormatoPcm f) =>
        f.Taxa == 96000 && f.Canais == 1 ? f with { Taxa = 48000, Canais = 2 } : f;
}

public static class FilaAudio
{
    public static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// Captura e reprodução têm relógios diferentes e a fila cresce devagar. Passou do limite,
    /// joga fora: perder 60 ms de som é melhor que o som ficar atrás da imagem.
    /// </summary>
    public static bool DeveLimpar(TimeSpan acumulado) => acumulado > Limite;
}

public static class SaidaAudio
{
    /// <summary>A saída fixada, se ainda existe; senão a padrão do Windows.</summary>
    public static string Resolver(string? fixaId, IReadOnlyCollection<string> disponiveis, string padraoId) =>
        fixaId is not null && disponiveis.Contains(fixaId) ? fixaId : padraoId;
}

public static class EntradaSom
{
    /// <summary>Valor guardado na configuração para "sem som".</summary>
    public const string Nenhuma = "nenhuma";

    /// <summary>
    /// null = automático: a entrada do mesmo aparelho USB que a placa. "nenhuma" = sem som.
    /// Uma entrada escolhida que sumiu volta ao automático (como a saída fixada que some volta à padrão).
    /// </summary>
    public static DispositivoAudio? Resolver(string? escolha, IReadOnlyList<DispositivoAudio> entradas, DispositivoVideo? placa)
    {
        if (escolha == Nenhuma) return null;
        if (escolha is not null && entradas.FirstOrDefault(e => e.Id == escolha) is { } fixa) return fixa;
        if (placa is null || IdUsb.De(placa.Link) is not { } video) return null;
        return entradas.FirstOrDefault(e => IdUsb.De(e.Hardware) is { } a && a.MesmoAparelho(video));
    }
}
