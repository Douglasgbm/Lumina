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
                File.AppendAllText(Path.Combine(Pasta, arquivo), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {linha}{Environment.NewLine}");
            }
            catch (IOException) { }
        }
    }

    public static void Log(string linha) => Escrever("lumina.log", linha);

    public static void Erro(string onde, Exception e) => Log($"[{onde}] {e.GetType().Name}: {e.Message}");

    public static void Diagnostico(string linha) => Escrever("diagnostico.txt", linha);
}
