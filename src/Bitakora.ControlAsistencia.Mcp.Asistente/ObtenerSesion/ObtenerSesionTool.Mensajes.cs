using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerSesion;

public partial class ObtenerSesionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerSesion.ObtenerSesionToolMensajes",
        typeof(ObtenerSesionTool).Assembly);

    internal static class Mensajes
    {
        public static string SinSesionDeUsuario =>
            ResourceManager.GetString(nameof(SinSesionDeUsuario))!;
    }
}
