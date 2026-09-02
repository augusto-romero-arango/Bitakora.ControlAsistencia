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
        // Fallback, ya no interino en el camino con Bearer (issue #572): IdentidadTenantMcpMiddleware
        // deriva tenant y usuario del token del usuario autenticado. Este valor fijo por despliegue
        // solo se usa cuando la invocacion no trae Bearer -- llamada directa con system key: smoke,
        // desarrollo local.
        var identidad = new IdentidadTenant(
            TenantId: configuration["Identidad:TenantIdInterino"] ?? "tenant-interino-sin-configurar",
            UserId: configuration["Identidad:UserIdInterino"] ?? "mcp-sin-usuario-autenticado");

        services.AddSingleton(identidad);
        services.AddTransient<PropagadorIdentidadTenantHandler>();
        services.AddSingleton<IDerivadorIdentidadTenantMcp, DerivadorIdentidadTenantMcp>();

        return services;
    }
}
