using Bitakora.ControlAsistencia.TenantResolver;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Puebla la identidad ambiente (TenantExecutionContext) para invocaciones de tool MCP, analogo a
// IdentidadTenantMcpMiddleware de Mcp.Consultas (issue #540). Lee el Authorization del
// ToolInvocationContext y no de FunctionContext.GetHttpContext(): una invocacion por
// McpToolTrigger no llega al worker con HttpContext -- el endpoint del protocolo lo sirve el
// paquete del host (ver AutorizacionMcpMiddleware, "LIMITE ESTRUCTURAL").
public sealed class IdentidadTenantMcpMiddleware(
    IValidadorTokenAuthKit validador, IDerivadorIdentidadTenantMcp derivador) : IFunctionsWorkerMiddleware
{
    internal const string EncabezadoAutorizacion = "Authorization";
    internal const string EsquemaBearer = "Bearer ";

    // Valor de Microsoft.Azure.Functions.Worker.Extensions.Mcp.Constants.McpToolTriggerBindingType,
    // internal a ese ensamblado: se replica el literal porque no hay forma de referenciarlo.
    private const string McpToolTriggerBindingType = "mcpToolTrigger";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var identidad = await DerivarIdentidadAsync(
            await LeerEncabezadoAutorizacionAsync(context), context.CancellationToken);

        // La mutacion del AsyncLocal va aqui y no dentro de un metodo async propio: el CLR restaura
        // el contexto de ejecucion al retornar de un metodo async, asi que una mutacion hecha
        // adentro no la ve el llamador -- ni siquiera cuando el await interno resuelve sincronico.
        if (identidad is not null)
            TenantExecutionContext.SetDerivedIdentity(identidad.TenantId, identidad.UserId);

        await next(context);
    }

    // Nucleo testable (mismo patron que AutorizacionMcpMiddleware.Invoke): opera sobre el
    // encabezado ya extraido, nunca sobre FunctionContext/ToolInvocationContext -- inalcanzables en
    // un unit test de nivel 1 (MEF-ADR-0048 seccion 1). Sin Bearer no hay identidad que derivar y
    // retorna null: el propagador cae al tenant fijo de ConfiguracionIdentidadTenant.
    internal async Task<IdentidadTenant?> DerivarIdentidadAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default)
    {
        if (encabezadoAutorizacion is null ||
            !encabezadoAutorizacion.StartsWith(EsquemaBearer, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = encabezadoAutorizacion[EsquemaBearer.Length..];
        var principal = await validador.ValidarAsync(token, cancellationToken);
        return principal is null ? null : derivador.Derivar(principal);
    }

    private static async Task<string?> LeerEncabezadoAutorizacionAsync(FunctionContext context)
    {
        if (context.FunctionDefinition.InputBindings.Values
                .FirstOrDefault(b => b.Type == McpToolTriggerBindingType) is not { } bindingMcp)
            return null;

        var invocacion = (await context.BindInputAsync<ToolInvocationContext>(bindingMcp)).Value;
        return invocacion is not null &&
               invocacion.TryGetHttpTransport(out var transporte) &&
               transporte is not null &&
               transporte.Headers.TryGetValue(EncabezadoAutorizacion, out var valor)
            ? valor
            : null;
    }
}
