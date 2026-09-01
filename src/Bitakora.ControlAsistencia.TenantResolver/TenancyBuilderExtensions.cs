using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;

namespace Bitakora.ControlAsistencia.TenantResolver;

public static class TenancyBuilderExtensions
{
    /// <summary>
    /// Registra <see cref="TenantContextMiddleware"/>, que puebla la identidad ambiente desde el
    /// trigger (headers HTTP del gateway o ApplicationProperties del mensaje de Service Bus).
    /// </summary>
    /// <remarks>
    /// Es la mitad de nivel builder de la tenancy; la otra mitad -- el registro de
    /// <c>ITenantResolver</c> en DI -- vive en
    /// <see cref="TenancyServiceCollectionExtensions.AgregarTenantResolverControlAsistencia"/>, dentro
    /// del seam de composicion del dominio. Se necesitan las dos: sin este middleware el resolver
    /// resuelve pero nunca se puebla.
    /// </remarks>
    public static IFunctionsWorkerApplicationBuilder UsarTenantContextMiddleware(
        this IFunctionsWorkerApplicationBuilder builder)
    {
        builder.UseMiddleware<TenantContextMiddleware>();
        return builder;
    }
}
