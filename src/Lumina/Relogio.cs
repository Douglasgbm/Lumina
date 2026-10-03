using System.Diagnostics;

namespace Lumina;

static class Relogio
{
    /// <summary>
    /// Relógio do PC (QPC) em 100 ns. O tempo da amostra da placa usa a mesma base:
    /// medido em 03/10/2026, 1º quadro com tempo 2248527147211 e relógio 2248527567580.
    /// </summary>
    public static long Agora100ns() => (long)(Stopwatch.GetTimestamp() * (10_000_000.0 / Stopwatch.Frequency));
}
