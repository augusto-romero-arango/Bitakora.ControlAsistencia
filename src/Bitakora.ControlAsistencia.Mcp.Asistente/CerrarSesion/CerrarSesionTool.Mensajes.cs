using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CerrarSesion;

public partial class CerrarSesionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.CerrarSesion.CerrarSesionToolMensajes",
        typeof(CerrarSesionTool).Assembly);

    internal static class Mensajes
    {
        public static string AbreElEnlace => ResourceManager.GetString(nameof(AbreElEnlace))!;
        public static string NotaCierre => ResourceManager.GetString(nameof(NotaCierre))!;
        public static string SinSesionQueCerrar => ResourceManager.GetString(nameof(SinSesionQueCerrar))!;
    }
}
