using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed partial class IdentidadTenantMcpMiddleware(
    IValidadorTokenAuthKit validador, IDerivadorIdentidadTenantMcp derivador) : IFunctionsWorkerMiddleware
{
    internal const string EncabezadoAutorizacion = "Authorization";
    internal const string EsquemaBearer = "Bearer ";

    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next) => throw new NotImplementedException();

    internal Task<IdentidadTenant?> DerivarIdentidadAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
