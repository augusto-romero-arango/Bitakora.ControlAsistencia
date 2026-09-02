using Bitakora.ControlAsistencia.TenantResolver;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

// Puebla la identidad ambiente (TenantExecutionContext, Bitakora.ControlAsistencia.TenantResolver
// -- segundo consumidor, reuso legitimo) para invocaciones de tool MCP: analogo a
// TenantContextMiddleware de los dominios, pero leyendo el encabezado Authorization del
// ToolInvocationContext (Transport/HttpTransport.Headers de la extension MCP 1.6.0) en vez de
// FunctionContext.GetHttpContext(), inalcanzable en un McpToolTrigger (ver AutorizacionMcpMiddleware,
// "LIMITE ESTRUCTURAL").
//
// GATE NO VERIFICADO (primera tarea del implementer, issue #540): que HttpTransport.Headers
// contenga efectivamente el header Authorization del request del protocolo -- el host podria
// filtrarlo. Plan B documentado en el issue si falla: estampar org_id/sub como
// X-Tenant-Id/X-User-Id en la politica APIM (patron MEF-ADR-0032 seccion 4) y leer esos headers en
// vez de revalidar aqui.
public sealed partial class IdentidadTenantMcpMiddleware(
    IValidadorTokenAuthKit validador, IDerivadorIdentidadTenantMcp derivador) : IFunctionsWorkerMiddleware
{
    internal const string EncabezadoAutorizacion = "Authorization";
    internal const string EsquemaBearer = "Bearer ";

    // Valor real de Microsoft.Azure.Functions.Worker.Extensions.Mcp.Constants.McpToolTriggerBindingType
    // (internal a ese ensamblado, ver "GATE NO VERIFICADO" arriba): mismo patron que
    // TenantContextMiddleware usa para localizar el binding de Service Bus por su Type.
    private const string McpToolTriggerBindingType = "mcpToolTrigger";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        string? encabezadoAutorizacion = null;

        if (context.FunctionDefinition.InputBindings.Values
                .FirstOrDefault(b => b.Type == McpToolTriggerBindingType) is { } bindingMcp)
        {
            var invocacion = (await context.BindInputAsync<ToolInvocationContext>(bindingMcp)).Value;
            if (invocacion is not null &&
                invocacion.TryGetHttpTransport(out var transport) &&
                transport is not null &&
                transport.Headers.TryGetValue(EncabezadoAutorizacion, out var valor))
            {
                encabezadoAutorizacion = valor;
            }
        }

        await PoblarIdentidadAmbienteAsync(encabezadoAutorizacion, context.CancellationToken);
        await next(context);
    }

    // Nucleo testable (mismo patron que AutorizacionMcpMiddleware.AutorizarAsync): opera sobre el
    // encabezado ya extraido, nunca sobre FunctionContext/ToolInvocationContext -- inalcanzables en
    // un unit test de nivel 1 (MEF-ADR-0048 seccion 1). CA-1: bearer con org_id -> puebla el
    // ambiente con TenantId=org_id/UserId=sub. CA-2: bearer sin org_id -> propaga el rechazo del
    // derivador. CA-3: sin bearer -> no toca el ambiente (el propagador cae al fallback fijo de
    // ConfiguracionIdentidadTenant).
    //
    // Deliberadamente SIN "async"/"await": TenantExecutionContext respalda su estado en un
    // AsyncLocal, y el CLR aisla las mutaciones de AsyncLocal hechas dentro de un metodo "async"
    // de quien lo invoca -- ExecutionContextSwitcher restaura el contexto previo al retornar de
    // CADA invocacion del state machine, incluso cuando el await interno resuelve de forma
    // sincronica (verificado empiricamente: un Task.FromResult ya completado igual pierde la
    // mutacion). Bloquear con GetAwaiter().GetResult() evita ese boundary -- el metodo corre
    // enteramente en el marco de pila de quien lo llama (Invoke, o el test), sin punto de
    // suspension propio, asi que la mutacion queda visible tanto en el continuation de Invoke
    // (await next(context)) como en el llamador del unit test. Es seguro bloquear aqui: el worker
    // aislado no tiene SynchronizationContext que serialice continuaciones (a diferencia de
    // ASP.NET Core clasico), asi que no hay riesgo de deadlock.
    internal Task PoblarIdentidadAmbienteAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default)
    {
        if (encabezadoAutorizacion is null ||
            !encabezadoAutorizacion.StartsWith(EsquemaBearer, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var token = encabezadoAutorizacion[EsquemaBearer.Length..];
        var principal = validador.ValidarAsync(token, cancellationToken).GetAwaiter().GetResult();
        var identidad = derivador.Derivar(principal);

        TenantExecutionContext.SetDerivedIdentity(identidad.TenantId, identidad.UserId);
        return Task.CompletedTask;
    }
}
