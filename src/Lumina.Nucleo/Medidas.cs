namespace Lumina.Nucleo;

/// <summary>Conta quadros e devolve o fps a cada segundo completo.</summary>
public sealed class ContadorQuadros
{
    TimeSpan? _inicio;
    int _quadros;

    /// <summary>Registra um quadro no instante dado. Devolve o fps quando fecha 1 s; senão null.</summary>
    public double? Registrar(TimeSpan agora)
    {
        if (_inicio is null)
        {
            _inicio = agora;
            _quadros = 0;
            return null;
        }
        _quadros++;
        var passado = agora - _inicio.Value;
        if (passado < TimeSpan.FromSeconds(1)) return null;
        double fps = _quadros / passado.TotalSeconds;
        _inicio = agora;
        _quadros = 0;
        return fps;
    }
}

public static class Frequencia
{
    /// <summary>Abaixo disso é silêncio ou chiado, não um tom.</summary>
    const float AmplitudeMinima = 0.01f;

    /// <summary>Frequência de um tom puro pelas passagens por zero subindo; null se não houver tom.</summary>
    public static double? Estimar(ReadOnlySpan<float> amostras, int taxa)
    {
        float pico = 0f;
        foreach (var s in amostras) pico = Math.Max(pico, Math.Abs(s));
        if (pico < AmplitudeMinima) return null;

        int primeira = -1, ultima = -1, subidas = 0;
        for (int i = 1; i < amostras.Length; i++)
        {
            if (amostras[i - 1] < 0f && amostras[i] >= 0f)
            {
                if (primeira < 0) primeira = i;
                ultima = i;
                subidas++;
            }
        }
        if (subidas < 3) return null;
        return (subidas - 1) * (double)taxa / (ultima - primeira);
    }
}

/// <summary>Junta atrasos em ms e devolve mediana, p95 e máximo a cada N amostras.</summary>
public sealed class ResumoAtraso(int amostrasPorResumo = 120)
{
    public readonly record struct Resumo(double Mediana, double P95, double Maximo);

    readonly List<double> _ms = new();

    public Resumo? Registrar(double ms)
    {
        _ms.Add(ms);
        if (_ms.Count < amostrasPorResumo) return null;
        _ms.Sort();
        var r = new Resumo(Percentil(0.50), Percentil(0.95), _ms[^1]);
        _ms.Clear();
        return r;
    }

    /// <summary>Posto mais próximo: o menor valor com pelo menos p das amostras abaixo ou iguais.</summary>
    double Percentil(double p) => _ms[(int)Math.Ceiling(p * _ms.Count) - 1];
}
