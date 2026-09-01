using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

// Protege el endpoint del protocolo MCP con un Bearer JWT de AuthKit (issue #554): desvio
// documentado de MEF-ADR-0047 (que fija la system key `mcp_extension` como unica credencial de un
// servidor MCP) -- la key se conserva como defensa en profundidad, este middleware suma la
// identidad de usuario que #540 necesita. "ready"/"version" y el propio documento PRM quedan fuera
// del gate; esa decision de enrutamiento vive en Invoke y no tiene unit test directo porque
// FunctionContext no es fakeable (mismo limite estructural que MEF-ADR-0048 seccion 1 documenta
// para el registro de tools/list).
public sealed partial class AutorizacionMcpMiddleware(IValidadorTokenAuthKit validador, Uri prmUri)
    : IFunctionsWorkerMiddleware
{
    internal const string EncabezadoAutorizacion = "Authorization";
    internal const string EncabezadoWwwAuthenticate = "WWW-Authenticate";
    internal const string EsquemaBearer = "Bearer ";

    // "ready"/"version" (sin identidad de usuario que proteger) y el propio PRM (debe leerse
    // anonimamente, spec de autorizacion MCP) quedan fuera del gate.
    private static readonly HashSet<string> FuncionesExentas =
        new(StringComparer.Ordinal) { "ready", "version", "MetadataRecursoProtegido" };

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null || FuncionesExentas.Contains(context.FunctionDefinition.Name))
        {
            await next(context);
            return;
        }

        var principal = await AutorizarAsync(httpContext, context.CancellationToken);
        if (principal is null)
            return;

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
        if (!encabezado.StartsWith(EsquemaBearer, StringComparison.Ordinal))
        {
            EscribirNoAutorizado(httpContext, Mensajes.TokenAusente);
            return null;
        }

        var token = encabezado[EsquemaBearer.Length..];
        try
        {
            return await validador.ValidarAsync(token, cancellationToken);
        }
        catch (SecurityTokenException)
        {
            EscribirNoAutorizado(httpContext, Mensajes.TokenInvalido);
            return null;
        }
    }

    private void EscribirNoAutorizado(HttpContext httpContext, string mensaje)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.Headers[EncabezadoWwwAuthenticate] =
            $"Bearer error=\"invalid_token\", error_description=\"{mensaje}\", resource_metadata=\"{prmUri}\"";
    }
}
