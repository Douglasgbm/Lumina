namespace Lumina.Nucleo;

/// <summary>
/// Fila do modo suave: guarda quadros com o carimbo de tempo da placa e, a cada batida do monitor,
/// entrega o mais novo que já "venceu" (carimbo ≤ agora − atraso). Os trancos de chegada da placa
/// ficam escondidos dentro do atraso (medido em 03/10/2026: pares colados e buracos de ~33 ms).
/// Itens devolvidos (descartados, despejados, esvaziados) são reaproveitados por quem chama.
/// Carimbos em 100 ns. Não é thread-safe: quem chama trava.
/// </summary>
public sealed class Agenda<T>(int capacidade)
{
    readonly Queue<(long Carimbo, T Item)> _fila = new();

    public int Quantidade => _fila.Count;

    /// <summary>
    /// Guarda o quadro. Verdadeiro se a fila estava cheia e o mais velho saiu (em despejado).
    /// Bool explícito: com índices (int), "nada despejado" não pode ser confundido com o índice 0.
    /// </summary>
    public bool Adicionar(long carimbo, T item, out T despejado)
    {
        despejado = default!;
        bool cheia = _fila.Count >= capacidade;
        if (cheia) despejado = _fila.Dequeue().Item;
        _fila.Enqueue((carimbo, item));
        return cheia;
    }

    /// <summary>O mais novo já vencido, e os mais velhos que ele (que não serão mais mostrados); null se nenhum venceu.</summary>
    public (T Item, List<T> Descartados)? Escolher(long agora, long atraso)
    {
        long limite = agora - atraso;
        if (_fila.Count == 0 || _fila.Peek().Carimbo > limite) return null;
        var descartados = new List<T>();
        var escolhido = _fila.Dequeue().Item;
        while (_fila.Count > 0 && _fila.Peek().Carimbo <= limite)
        {
            descartados.Add(escolhido);
            escolhido = _fila.Dequeue().Item;
        }
        return (escolhido, descartados);
    }

    public List<T> Esvaziar()
    {
        var todos = _fila.Select(x => x.Item).ToList();
        _fila.Clear();
        return todos;
    }
}

public static class AtrasoSuave
{
    /// <summary>Antes da primeira medição: cobre o p95 de 27–39 ms medido em 03/10/2026.</summary>
    public const double Inicial = 35.0;

    /// <summary>p95 da chegada + 4 ms de folga, entre 10 e 80 ms.</summary>
    public static double De(ResumoAtraso.Resumo chegada) => Math.Clamp(chegada.P95 + 4, 10, 80);
}
