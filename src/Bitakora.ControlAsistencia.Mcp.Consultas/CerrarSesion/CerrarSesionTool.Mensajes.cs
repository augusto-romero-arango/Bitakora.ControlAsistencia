using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.CerrarSesion;

public partial class CerrarSesionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.CerrarSesion.CerrarSesionToolMensajes",
        typeof(CerrarSesionTool).Assembly);

    internal static class Mensajes
    {
        public static string AbreElEnlace => ResourceManager.GetString(nameof(AbreElEnlace))!;
        public static string NotaCierre => ResourceManager.GetString(nameof(NotaCierre))!;
        public static string SinSesionQueCerrar => ResourceManager.GetString(nameof(SinSesionQueCerrar))!;
    }
}
