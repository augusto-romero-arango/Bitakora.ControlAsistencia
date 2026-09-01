using Azure.Messaging.ServiceBus;
using Cosmos.MultiTenancy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.TenantResolver;

/// <summary>
/// Puebla el <see cref="TenantExecutionContext"/> de la invocacion, leyendo la identidad del contexto
/// de ejecucion del trigger:
/// <list type="bullet">
///   <item>HTTP: headers confiables X-Tenant-Id/X-User-Id (via <c>FunctionContext.GetHttpContext()</c>).</item>
///   <item>Service Bus: ApplicationProperties tenant-id/user_id del mensaje, obtenido tipado con
///     <c>BindInputAsync</c> (la maquinaria de binding cachea el resultado, no re-convierte ni
///     settlea el mensaje).</item>
/// </list>
/// Se puebla via <see cref="TenantExecutionContext.Set"/> (estatico, respaldado por AsyncLocal), sin
/// resolver nada del contenedor: el estado es ambiente, no depende del scope de DI de la invocacion.
///
/// Es el unico punto de poblacion para los triggers que reciben la identidad del gateway o del
/// mensaje. Los que no la reciben -- webhooks de un proveedor externo, que no pueden presentar un JWT
/// -- la derivan de su payload ya verificado y la pueblan con
/// <see cref="TenantExecutionContext.SetDerivedIdentity"/>.
/// </summary>
public sealed class TenantContextMiddleware : IFunctionsWorkerMiddleware
{
    // Headers confiables que estampa el gateway APIM (MEF-ADR-0032).
    private const string TenantHeaderHttp = "X-Tenant-Id";
    private const string UserHeaderHttp = "X-User-Id";

    // Llave con que Wolverine serializa el TenantId del envelope a ApplicationProperties
    // (Wolverine.EnvelopeConstants.TenantIdKey). El user_id viaja bajo Cosmos.MultiTenancy.TenancyHeaders.UserId.
    private const string TenantPropertyAsb = "tenant-id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is not null)
        {
            TenantExecutionContext.Set(
                httpContext.Request.Headers[TenantHeaderHttp],
                httpContext.Request.Headers[UserHeaderHttp]);
        }
        else if (context.FunctionDefinition.InputBindings.Values
                     .FirstOrDefault(b => b.Type == "serviceBusTrigger") is { } serviceBusBinding)
        {
            var message = (await context.BindInputAsync<ServiceBusReceivedMessage>(serviceBusBinding)).Value;
            if (message is not null)
            {
                TenantExecutionContext.Set(
                    Leer(message.ApplicationProperties, TenantPropertyAsb),
                    Leer(message.ApplicationProperties, TenancyHeaders.UserId));
            }
        }

        await next(context);
    }

    private static string? Leer(IReadOnlyDictionary<string, object> propiedades, string llave)
        => propiedades.TryGetValue(llave, out var valor) ? valor?.ToString() : null;
}
