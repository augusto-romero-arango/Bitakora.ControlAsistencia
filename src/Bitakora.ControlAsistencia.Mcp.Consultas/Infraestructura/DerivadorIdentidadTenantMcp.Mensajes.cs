using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public sealed partial class DerivadorIdentidadTenantMcp
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura.DerivadorIdentidadTenantMcpMensajes",
        typeof(DerivadorIdentidadTenantMcp).Assembly);

    internal static class Mensajes
    {
        public static string OrganizacionAusente => ResourceManager.GetString(nameof(OrganizacionAusente))!;

        public static string UsuarioAusente => ResourceManager.GetString(nameof(UsuarioAusente))!;
    }
}
