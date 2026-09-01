using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public sealed partial class AutorizacionMcpMiddleware
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura.AutorizacionMcpMiddlewareMensajes",
        typeof(AutorizacionMcpMiddleware).Assembly);

    internal static class Mensajes
    {
        public static string TokenAusente => ResourceManager.GetString(nameof(TokenAusente))!;

        public static string TokenInvalido => ResourceManager.GetString(nameof(TokenInvalido))!;
    }
}
