using Cosmos.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bitakora.ControlAsistencia.TenantResolver;

public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="TenantExecutionContext"/> como <see cref="ITenantContext"/> singleton: la
    /// identidad vive en un AsyncLocal ambiente, no en la instancia.
    /// </summary>
    /// <remarks>
    /// Va dentro del seam <c>ComposicionServicios</c> de cada dominio porque los routers y senders de
    /// Wolverine dependen de <see cref="ITenantContext"/>. Quien lo llame debe registrar ademas
    /// <see cref="TenancyBuilderExtensions.UsarTenantContextMiddleware"/>, que puebla el ambiente.
    /// </remarks>
    public static IServiceCollection AgregarTenantResolverControlAsistencia(this IServiceCollection services)
    {
        services.RemoveAll<ITenantContext>();
        services.AddSingleton<ITenantContext, TenantExecutionContext>();
        return services;
    }
}
