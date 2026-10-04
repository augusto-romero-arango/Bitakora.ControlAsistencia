using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Seam de composicion de la identidad fija de fallback, del derivador y del propagador que la inyecta en cada
/// HttpClient tipado (MEF-ADR-0029, MEF-ADR-0047 decision 6).
/// </summary>
public static class ConfiguracionIdentidadTenant
{
    public static IServiceCollection ConfigurarIdentidadTenant(this IServiceCollection services, IConfiguration configuration)
    {
        // Fallback del camino sin Bearer (llamada directa con system key: smoke, desarrollo local):
        // con Bearer, IdentidadTenantMcpMiddleware deriva la identidad del token del usuario
        // autenticado y el propagador la prefiere sobre este valor fijo por despliegue.
        var identidad = new IdentidadTenant(
            TenantId: configuration["Identidad:TenantIdInterino"] ?? "tenant-interino-sin-configurar",
            UserId: configuration["Identidad:UserIdInterino"] ?? "mcp-sin-usuario-autenticado",
            OrganizationMembershipId: configuration["Identidad:OrganizationMembershipIdInterino"] ?? "membership-sin-configurar");

        services.AddSingleton(identidad);
        services.AddTransient<PropagadorIdentidadTenantHandler>();
        services.AddSingleton<IDerivadorIdentidadTenantMcp, DerivadorIdentidadTenantMcp>();

        return services;
    }
}
