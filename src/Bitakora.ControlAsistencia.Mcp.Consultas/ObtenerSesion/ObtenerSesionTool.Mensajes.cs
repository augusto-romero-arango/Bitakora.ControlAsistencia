using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerSesion;

public partial class ObtenerSesionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerSesion.ObtenerSesionToolMensajes",
        typeof(ObtenerSesionTool).Assembly);

    internal static class Mensajes
    {
        public static string SinSesionDeUsuario =>
            ResourceManager.GetString(nameof(SinSesionDeUsuario))!;
    }
}
