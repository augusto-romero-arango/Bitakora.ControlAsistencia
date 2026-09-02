using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Defensa en profundidad, NUNCA el gate primario (MEF-ADR-0047 decision 7): las tool calls de un
/// cliente MCP contra /runtime/webhooks/mcp llegan a este worker SIN header Authorization -- lo
/// sirve el paquete del host de la extension MCP, que no lo reenvia. Intentar exigirlo aqui
/// produce, en el mejor caso, un gate que nunca se activa, y en el peor, un rechazo universal
/// porque el header buscado no existe nunca en ese punto. El gate real vive en la politica
/// dedicada de APIM (MEF-ADR-0032 seccion 9). Este middleware solo registra, con Warning, un
/// Authorization presente pero invalido en las superficies HTTP que si lo reciben (p. ej. un
/// endpoint propio fuera del protocolo MCP) -- nunca bloquea el pipeline.
/// </summary>
public sealed class AutorizacionMcpMiddleware(
    IValidadorTokenAuthKit validador,
    ILogger<AutorizacionMcpMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // GetHttpContext(), no GetHttpRequestDataAsync(): este proyecto usa la integracion ASP.NET
        // Core, el mismo acceso a headers que TenantContextMiddleware en el BC (MEF-ADR-0028
        // seccion 4). Devuelve null en cualquier invocacion que no venga de un trigger HTTP.
        var authorizationHeader = context.GetHttpContext()?.Request.Headers.Authorization.FirstOrDefault();
        var token = authorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorizationHeader["Bearer ".Length..]
            : null;

        if (!string.IsNullOrWhiteSpace(token) && !await validador.EsValidoAsync(token, context.CancellationToken))
            logger.LogWarning(
                "Token Authorization presente pero invalido en {Funcion} -- defensa en profundidad, el request continua: el gate real es la politica de APIM (MEF-ADR-0032 seccion 9).",
                context.FunctionDefinition.Name);

        await next(context);
    }
}
