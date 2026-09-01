using System.Security.Claims;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

// Protege con un Bearer JWT de AuthKit (issue #554) las Functions HTTP de este worker que exponen
// identidad de usuario: desvio documentado de MEF-ADR-0047 (que fija la system key
// `mcp_extension` como unica credencial de un servidor MCP) -- la key se conserva como defensa en
// profundidad, este middleware suma la identidad que #540 necesita.
//
// LIMITE ESTRUCTURAL (verificado, ver reporte del reviewer del issue #554): una invocacion por
// McpToolTrigger NO llega aqui con HttpContext -- el endpoint del protocolo (/runtime/webhooks/mcp)
// lo sirve el paquete del HOST, y el worker solo recibe los argumentos de la tool, sin el header
// Authorization. Es el mismo limite que MEF-ADR-0048 seccion 1 documenta para tools/list. Mientras
// las tools no pasen por una Function HTTP propia, el gate de CA-1 debe vivir delante del host
// (APIM con validate-jwt, MEF-ADR-0032) -- no aqui.
public sealed partial class AutorizacionMcpMiddleware(
    IValidadorTokenAuthKit validador, UriMetadataRecursoProtegido prm) : IFunctionsWorkerMiddleware
{
    internal const string EncabezadoAutorizacion = "Authorization";
    internal const string EncabezadoWwwAuthenticate = "WWW-Authenticate";
    internal const string EsquemaBearer = "Bearer ";

    // "ready"/"version" (sin identidad de usuario que proteger) y el propio PRM (debe leerse
    // anonimamente, spec de autorizacion MCP) quedan fuera del gate.
    private static readonly HashSet<string> FuncionesExentas =
        new(StringComparer.Ordinal) { "ready", "version", FunctionEndpoint.NombreFuncion };

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null || FuncionesExentas.Contains(context.FunctionDefinition.Name))
        {
            await next(context);
            return;
        }

        if (await AutorizarAsync(httpContext, context.CancellationToken) is not null)
            await next(context);
    }

    // Nucleo testable de la decision de autorizacion: opera sobre HttpContext (fakeable con
    // DefaultHttpContext), nunca sobre FunctionContext. CA-2: token valido -> retorna el
    // ClaimsPrincipal (issuer+firma+expiracion ya verificados por el validador). CA-1/CA-4: sin
    // Bearer o token invalido -- escribe 401 + WWW-Authenticate apuntando al PRM, retorna null.
    internal async Task<ClaimsPrincipal?> AutorizarAsync(
        HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var encabezado = httpContext.Request.Headers[EncabezadoAutorizacion].ToString();
        // El nombre del esquema es case-insensitive (RFC 9110 seccion 11.1), a diferencia del
        // token que lo sigue.
        if (!encabezado.StartsWith(EsquemaBearer, StringComparison.OrdinalIgnoreCase))
            return NoAutorizado(httpContext, Mensajes.TokenAusente);

        var token = encabezado[EsquemaBearer.Length..];
        if (string.IsNullOrWhiteSpace(token))
            return NoAutorizado(httpContext, Mensajes.TokenAusente);

        try
        {
            return await validador.ValidarAsync(token, cancellationToken);
        }
        catch (SecurityTokenException)
        {
            return NoAutorizado(httpContext, Mensajes.TokenInvalido);
        }
    }

    private ClaimsPrincipal? NoAutorizado(HttpContext httpContext, string mensaje)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.Headers[EncabezadoWwwAuthenticate] =
            $"Bearer error=\"invalid_token\", error_description=\"{mensaje}\", resource_metadata=\"{prm}\"";
        return null;
    }
}
