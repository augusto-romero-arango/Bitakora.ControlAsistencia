using Microsoft.Azure.Functions.Worker;
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

    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next) =>
        throw new NotImplementedException();

    // Nucleo testable (mismo patron que AutorizacionMcpMiddleware.Invoke): opera sobre el
    // encabezado ya extraido, nunca sobre FunctionContext/ToolInvocationContext -- inalcanzables en
    // un unit test de nivel 1 (MEF-ADR-0048 seccion 1). Sin Bearer no hay identidad que derivar y
    // retorna null: el propagador cae al tenant fijo de ConfiguracionIdentidadTenant.
    internal Task<IdentidadTenant?> DerivarIdentidadAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
