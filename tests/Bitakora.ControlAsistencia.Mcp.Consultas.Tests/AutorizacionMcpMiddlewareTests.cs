using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-1/CA-2/CA-4: nucleo testable de la decision de autorizacion (AutorizarAsync opera sobre
// HttpContext, fakeable con DefaultHttpContext). El fake ValidadorTokenFalso aisla esta decision
// de la criptografia real, cubierta en ValidadorTokenAuthKitTests -- mismo patron de "handler
// falso, sin red real" que MEF-ADR-0048 seccion 1 fija para el nivel 1.
public class AutorizacionMcpMiddlewareTests
{
    private static readonly UriMetadataRecursoProtegido PrmUri =
        new(new Uri("https://mcp-consultas.controlasistencia.example.com"));

    private static DefaultHttpContext ContextoConEncabezado(string? encabezado)
    {
        var contexto = new DefaultHttpContext();
        if (encabezado is not null)
            contexto.Request.Headers[AutorizacionMcpMiddleware.EncabezadoAutorizacion] = encabezado;
        return contexto;
    }

    private static DefaultHttpContext ContextoConBearer(string? token) =>
        ContextoConEncabezado(token is null ? null : $"{AutorizacionMcpMiddleware.EsquemaBearer}{token}");

    [Fact]
    public async Task AutorizarAsync_RetornaElPrincipalSinTocarLaRespuesta_CuandoElTokenEsValido()
    {
        var principalEsperado = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "usuario-mcp")]));
        var middleware = new AutorizacionMcpMiddleware(ValidadorTokenFalso.QueAutoriza(principalEsperado), PrmUri);
        var contexto = ContextoConBearer("token-valido");

        var resultado = await middleware.AutorizarAsync(contexto, TestContext.Current.CancellationToken);

        resultado.Should().BeSameAs(principalEsperado);
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task AutorizarAsync_Retorna401ConWwwAuthenticateHaciaElPrm_CuandoNoHayEncabezadoAuthorization()
    {
        var middleware = new AutorizacionMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenException("no deberia invocarse sin Bearer")),
            PrmUri);
        var contexto = ContextoConBearer(null);

        var resultado = await middleware.AutorizarAsync(contexto, TestContext.Current.CancellationToken);

        resultado.Should().BeNull();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        contexto.Response.Headers[AutorizacionMcpMiddleware.EncabezadoWwwAuthenticate].ToString().Should()
            .Contain($"resource_metadata=\"{PrmUri}\"")
            .And.Contain(AutorizacionMcpMiddleware.Mensajes.TokenAusente);
    }

    [Fact]
    public async Task AutorizarAsync_Retorna401ConWwwAuthenticateHaciaElPrm_CuandoElTokenEstaExpirado()
    {
        var middleware = new AutorizacionMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenExpiredException("expirado")), PrmUri);
        var contexto = ContextoConBearer("token-expirado");

        var resultado = await middleware.AutorizarAsync(contexto, TestContext.Current.CancellationToken);

        resultado.Should().BeNull();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        contexto.Response.Headers[AutorizacionMcpMiddleware.EncabezadoWwwAuthenticate].ToString().Should()
            .Contain($"resource_metadata=\"{PrmUri}\"")
            .And.Contain(AutorizacionMcpMiddleware.Mensajes.TokenInvalido);
    }

    [Fact]
    public async Task AutorizarAsync_Retorna401ConWwwAuthenticateHaciaElPrm_CuandoElEmisorEsIncorrecto()
    {
        var middleware = new AutorizacionMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenInvalidIssuerException("emisor incorrecto")), PrmUri);
        var contexto = ContextoConBearer("token-emisor-incorrecto");

        var resultado = await middleware.AutorizarAsync(contexto, TestContext.Current.CancellationToken);

        resultado.Should().BeNull();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        contexto.Response.Headers[AutorizacionMcpMiddleware.EncabezadoWwwAuthenticate].ToString().Should()
            .Contain($"resource_metadata=\"{PrmUri}\"")
            .And.Contain(AutorizacionMcpMiddleware.Mensajes.TokenInvalido);
    }

    [Fact]
    public async Task AutorizarAsync_RetornaElPrincipal_CuandoElNombreDelEsquemaVieneEnMinusculas()
    {
        var principalEsperado = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "usuario-mcp")]));
        var middleware = new AutorizacionMcpMiddleware(ValidadorTokenFalso.QueAutoriza(principalEsperado), PrmUri);
        var contexto = ContextoConEncabezado("bearer token-valido");

        var resultado = await middleware.AutorizarAsync(contexto, TestContext.Current.CancellationToken);

        resultado.Should().BeSameAs(principalEsperado);
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task AutorizarAsync_Retorna401ConWwwAuthenticateHaciaElPrm_CuandoElEsquemaBearerLlegaSinToken()
    {
        var middleware = new AutorizacionMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new InvalidOperationException("no deberia invocarse con un token vacio")),
            PrmUri);
        var contexto = ContextoConBearer("   ");

        var resultado = await middleware.AutorizarAsync(contexto, TestContext.Current.CancellationToken);

        resultado.Should().BeNull();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        contexto.Response.Headers[AutorizacionMcpMiddleware.EncabezadoWwwAuthenticate].ToString().Should()
            .Contain($"resource_metadata=\"{PrmUri}\"")
            .And.Contain(AutorizacionMcpMiddleware.Mensajes.TokenAusente);
    }
}
