using Cosmos.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bitakora.ControlAsistencia.TenantResolver;

public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="TenantExecutionContext"/> como <see cref="ITenantResolver"/> (MEF-ADR-0032,
    /// patron de la implementacion de referencia Cosmos.ControlPlane). Singleton concreto: la
    /// identidad vive en un AsyncLocal ambiente, no en la instancia, asi que un lector sin estado
    /// basta.
    /// </summary>
    /// <remarks>
    /// Va dentro del seam <c>ComposicionServicios</c> de cada dominio (MEF-ADR-0029) porque los
    /// routers y senders de Wolverine dependen de <see cref="ITenantResolver"/>: sin este registro el
    /// grafo de DI no se puede validar. Quien lo llame debe ademas registrar el middleware que puebla
    /// el ambiente, via <see cref="TenancyBuilderExtensions.UsarTenantContextMiddleware"/>.
    /// </remarks>
    public static IServiceCollection AgregarTenantResolverControlAsistencia(this IServiceCollection services)
    {
        services.RemoveAll<ITenantResolver>();
        services.AddSingleton<ITenantResolver, TenantExecutionContext>();
        return services;
    }
}
