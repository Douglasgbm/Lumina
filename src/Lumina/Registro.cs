namespace Lumina;

/// <summary>Arquivos de texto em %APPDATA%\Lumina: lumina.log (sempre) e diagnostico.txt (com --diagnostico).</summary>
static class Registro
{
    public static readonly string Pasta =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lumina");

    static readonly object Trava = new();

    public static void Escrever(string arquivo, string linha)
    {
        lock (Trava)
        {
            try
            {
                Directory.CreateDirectory(Pasta);
                var caminho = Path.Combine(Pasta, arquivo);
                // Passou de 5 MB: guarda o anterior como .1 e começa outro (sem limite, o log crescia para sempre).
                if (File.Exists(caminho) && new FileInfo(caminho).Length > 5 * 1024 * 1024)
                    File.Move(caminho, caminho + ".1", overwrite: true);
                File.AppendAllText(caminho, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {linha}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Log é ajuda, não pode derrubar o app (antivírus/arquivo somente leitura lançavam UnauthorizedAccess).
            }
        }
    }

    public static void Log(string linha) => Escrever("lumina.log", linha);

    public static void Erro(string onde, Exception e) => Log($"[{onde}] {e.GetType().Name}: {e.Message}");

    public static void Diagnostico(string linha) => Escrever("diagnostico.txt", linha);
}
