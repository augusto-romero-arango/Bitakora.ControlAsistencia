using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

public sealed partial class IdentidadTenantMcpMiddleware
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura.IdentidadTenantMcpMiddlewareMensajes",
        typeof(IdentidadTenantMcpMiddleware).Assembly);

    internal static class Mensajes
    {
        public static string TokenNoValidado => ResourceManager.GetString(nameof(TokenNoValidado))!;
    }
}
