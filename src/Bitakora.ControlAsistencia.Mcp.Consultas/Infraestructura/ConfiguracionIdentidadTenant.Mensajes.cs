using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public partial class ConfiguracionIdentidadTenant
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura.ConfiguracionIdentidadTenantMensajes",
        typeof(ConfiguracionIdentidadTenant).Assembly);

    internal static class Mensajes
    {
        public static string TenantIdAusente =>
            ResourceManager.GetString(nameof(TenantIdAusente))!;

        public static string UserIdAusente =>
            ResourceManager.GetString(nameof(UserIdAusente))!;
    }
}
