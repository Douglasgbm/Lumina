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
