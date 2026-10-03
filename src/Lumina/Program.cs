using Vortice.MediaFoundation;

namespace Lumina;

static class Program
{
    /// <summary>
    /// --diagnostico grava fps e frequência do som em %APPDATA%\Lumina\diagnostico.txt.
    /// --mudo começa sem som (teste de tom com a saída padrão apontando para a placa).
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        MediaFactory.MFStartup(true).CheckError();
        try
        {
            Application.Run(new JanelaPrincipal(args.Contains("--diagnostico"), args.Contains("--mudo")));
        }
        finally
        {
            MediaFactory.MFShutdown();
        }
    }
}
