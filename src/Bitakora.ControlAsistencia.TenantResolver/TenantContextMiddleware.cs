using Azure.Messaging.ServiceBus;
using Cosmos.MultiTenancy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.TenantResolver;

/// <summary>
/// Puebla el <see cref="TenantExecutionContext"/> de la invocacion desde los headers HTTP del gateway o
/// las ApplicationProperties del mensaje de Service Bus. Los triggers que no reciben esa identidad
/// usan <see cref="TenantExecutionContext.SetDerivedIdentity"/>.
/// </summary>
/// <remarks>
/// <c>BindInputAsync</c> cachea el mensaje enlazado: no lo re-convierte ni lo settlea.
/// </remarks>
public sealed class TenantContextMiddleware : IFunctionsWorkerMiddleware
{
    // Headers confiables que estampa el gateway APIM (MEF-ADR-0032).
    private const string TenantHeaderHttp = "X-Tenant-Id";
    private const string UserHeaderHttp = "X-User-Id";
    private const string MembershipHeaderHttp = "X-Organization-Membership-Id";

    // Debe casar con Wolverine.EnvelopeConstants.TenantIdKey, la llave con que Wolverine serializa el
    // TenantId del envelope a ApplicationProperties.
    private const string TenantPropertyAsb = "tenant-id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is not null)
        {
            PoblarDesdeHttp(h => httpContext.Request.Headers[h].ToString());
        }
        else if (context.FunctionDefinition.InputBindings.Values
                     .FirstOrDefault(b => b.Type == "serviceBusTrigger") is { } serviceBusBinding)
        {
            var message = (await context.BindInputAsync<ServiceBusReceivedMessage>(serviceBusBinding)).Value;
            if (message is not null)
            {
                PoblarDesdeServiceBus(message.ApplicationProperties);
            }
        }

        await next(context);
    }

    internal static void PoblarDesdeHttp(Func<string, string?> leerHeader)
        => TenantExecutionContext.Set(
            leerHeader(TenantHeaderHttp),
            leerHeader(UserHeaderHttp),
            leerHeader(MembershipHeaderHttp));

    internal static void PoblarDesdeServiceBus(IReadOnlyDictionary<string, object> applicationProperties)
        => TenantExecutionContext.Set(
            Leer(applicationProperties, TenantPropertyAsb),
            Leer(applicationProperties, TenancyHeaders.UserId),
            Leer(applicationProperties, TenancyHeaders.OrganizationMembershipId));

    private static string? Leer(IReadOnlyDictionary<string, object> propiedades, string llave)
        => propiedades.TryGetValue(llave, out var valor) ? valor?.ToString() : null;
}
