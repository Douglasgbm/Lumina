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

    /// <summary>Corrige valores fora do possível (arquivo editado à mão, versão antiga).</summary>
    public Configuracao Saneada() => this with
    {
        Largura = Math.Clamp(Largura, 320, 16384),
        Altura = Math.Clamp(Altura, 180, 16384),
        Volume = float.IsFinite(Volume) ? Math.Clamp(Volume, 0f, 1f) : 1f,
        Modo = ModoVideo.PorNome(Modo).Nome,
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
