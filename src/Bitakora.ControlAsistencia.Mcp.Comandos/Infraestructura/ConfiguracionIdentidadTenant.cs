using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Seam de composicion de la identidad interina y del propagador que la inyecta en cada
/// HttpClient tipado (MEF-ADR-0029, MEF-ADR-0047 decision 6).
/// </summary>
public static class ConfiguracionIdentidadTenant
{
    public static IServiceCollection ConfigurarIdentidadTenant(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO(tenancy etapa b / identidad derivada del token, MEF-ADR-0047 decision 6): el
        // worker no recibe el Authorization de una tool call (decision 7), asi que el tenant y el
        // usuario son un valor FIJO por despliegue, leido de app settings -- nunca derivado del
        // cliente MCP conectado. Reemplazarlo por identidad derivada del token es evolucion fuera
        // de alcance de este scaffold.
        var identidad = new IdentidadTenant(
            TenantId: configuration["Identidad:TenantIdInterino"] ?? "tenant-interino-sin-configurar",
            UserId: configuration["Identidad:UserIdInterino"] ?? "mcp-sin-usuario-autenticado");

        services.AddSingleton(identidad);
        services.AddTransient<PropagadorIdentidadTenantHandler>();

        return services;
    }
}
