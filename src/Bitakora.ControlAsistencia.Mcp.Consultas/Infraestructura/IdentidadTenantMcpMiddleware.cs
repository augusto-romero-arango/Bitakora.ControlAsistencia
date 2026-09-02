using Microsoft.Azure.Functions.Worker;
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

    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next) =>
        throw new NotImplementedException();

    // Nucleo testable (mismo patron que AutorizacionMcpMiddleware.AutorizarAsync): opera sobre el
    // encabezado ya extraido, nunca sobre FunctionContext/ToolInvocationContext -- inalcanzables en
    // un unit test de nivel 1 (MEF-ADR-0048 seccion 1). CA-1: bearer con org_id -> puebla el
    // ambiente con TenantId=org_id/UserId=sub. CA-2: bearer sin org_id -> propaga el rechazo del
    // derivador. CA-3: sin bearer -> no toca el ambiente (el propagador cae al fallback fijo de
    // ConfiguracionIdentidadTenant).
    internal Task PoblarIdentidadAmbienteAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
