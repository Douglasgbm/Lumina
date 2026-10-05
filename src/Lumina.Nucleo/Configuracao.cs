using System.Text.Json;

namespace Lumina.Nucleo;

/// <summary>Tudo que o Lumina lembra entre uma abertura e outra.</summary>
public sealed record Configuracao
{
    public int X { get; init; } = 100;
    public int Y { get; init; } = 100;
    public int Largura { get; init; } = 1280;
    public int Altura { get; init; } = 720;
    /// <summary>Maximizada pelo botão do Windows; X/Y/Largura/Altura guardam o tamanho de quando não está.</summary>
    public bool Maximizada { get; init; }
    public bool TelaCheia { get; init; }
    public string Modo { get; init; } = ModoVideo.Hd60.Nome;
    public string? SaidaFixaId { get; init; }
    public float Volume { get; init; } = 1f;
    public bool Mudo { get; init; }
    /// <summary>Mostra no ritmo do monitor com uma fila curta (como o OBS); falso = cada quadro na hora que chega.</summary>
    public bool ModoSuave { get; init; } = true;
    /// <summary>A placa escolhida no menu; null = nenhuma escolhida (usa a MS2109 se estiver conectada).</summary>
    public string? PlacaLink { get; init; }
    public string? PlacaNome { get; init; }
    /// <summary>null = automático (a do aparelho da placa); "nenhuma" = sem som; ou o id da entrada.</summary>
    public string? EntradaSom { get; init; }

    /// <summary>
    /// Corrige valores fora do possível (arquivo editado à mão, versão antiga). O modo não é conferido aqui:
    /// depende da placa — um nome que a placa não tem cai no padrão em Modos.Escolher.
    /// </summary>
    public Configuracao Saneada() => this with
    {
        X = Math.Clamp(X, -32000, 32000),
        Y = Math.Clamp(Y, -32000, 32000),
        Largura = Math.Clamp(Largura, 320, 16384),
        Altura = Math.Clamp(Altura, 180, 16384),
        Volume = float.IsFinite(Volume) ? Math.Clamp(Volume, 0f, 1f) : 1f,
        Modo = string.IsNullOrWhiteSpace(Modo) ? ModoVideo.Hd60.Nome : Modo,
    };
}

public sealed class ArmazemConfiguracao(string caminho)
{
    static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    /// <summary>Arquivo ausente, corrompido ou ilegível: volta ao padrão, nunca quebra.</summary>
    public Configuracao Ler()
    {
        try
        {
            if (!File.Exists(caminho)) return new Configuracao();
            var lida = JsonSerializer.Deserialize<Configuracao>(File.ReadAllText(caminho), Opcoes);
            return (lida ?? new Configuracao()).Saneada();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new Configuracao();
        }
    }

    /// <summary>Grava num .tmp e troca: uma queda de luz no meio não deixa o arquivo pela metade.</summary>
    public void Salvar(Configuracao c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
        var tmp = caminho + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, Opcoes));
        File.Move(tmp, caminho, overwrite: true);
    }
}
