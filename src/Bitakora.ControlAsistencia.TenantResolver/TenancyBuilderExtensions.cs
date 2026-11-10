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
    /// Va junto con <see cref="TenancyServiceCollectionExtensions.AgregarTenantResolverControlAsistencia"/>:
    /// sin este middleware el <c>ITenantContext</c> resuelve pero nunca se puebla.
    /// </remarks>
    public static IFunctionsWorkerApplicationBuilder UsarTenantContextMiddleware(
        this IFunctionsWorkerApplicationBuilder builder)
    {
        builder.UseMiddleware<TenantContextMiddleware>();
        return builder;
    }
}
