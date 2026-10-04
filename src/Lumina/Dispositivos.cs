using Lumina.Nucleo;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>As câmeras que o Windows enxerga agora (placas de captura e webcams, sem distinção).</summary>
static class Dispositivos
{
    public static List<DispositivoVideo> Video()
    {
        var lista = new List<DispositivoVideo>();
        using (var ativos = MediaFactory.MFEnumVideoDeviceSources())
            foreach (var a in ativos) lista.Add(new DispositivoVideo(a.FriendlyName, a.SymbolicLink));
        return lista;
    }
}
