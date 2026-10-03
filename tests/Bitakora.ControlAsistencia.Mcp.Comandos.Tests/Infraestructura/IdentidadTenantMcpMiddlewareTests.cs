using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Infraestructura;

// Nucleo testable de IdentidadTenantMcpMiddleware: DerivarIdentidadAsync opera sobre el encabezado
// Authorization ya extraido, porque FunctionContext/ToolInvocationContext no son instanciables en un
// unit test (MEF-ADR-0048 seccion 1). El validador y el derivador se fakean para aislar la
// orquestacion de la criptografia (ValidadorTokenAuthKitTests) y de la traduccion de claims
// (DerivadorIdentidadTenantMcpTests).
public class IdentidadTenantMcpMiddlewareTests
{
    private static ClaimsPrincipal PrincipalDeEjemplo =>
        new(new ClaimsIdentity([new Claim("sub", "usuario_123")]));

    [Fact]
    public async Task DerivarIdentidad_RetornaLaIdentidadDelToken_CuandoElBearerTraeOrganizacion()
    {
        var identidadEsperada = new IdentidadTenant("org_acme", "usuario_123", "membership-fijo");
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueAutoriza(PrincipalDeEjemplo),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(identidadEsperada),
            new LoggerFalso<IdentidadTenantMcpMiddleware>());

        var identidad = await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido",
            TestContext.Current.CancellationToken);

        identidad.Should().Be(identidadEsperada);
    }

    [Fact]
    public async Task DerivarIdentidad_PropagaElRechazoDelDerivador_CuandoElUsuarioNoTieneOrganizacion()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueAutoriza(PrincipalDeEjemplo),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException(DerivadorIdentidadTenantMcp.Mensajes.OrganizacionAusente)),
            new LoggerFalso<IdentidadTenantMcpMiddleware>());

        var act = async () => await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-sin-org",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{DerivadorIdentidadTenantMcp.Mensajes.OrganizacionAusente}*");
    }

    // Distinto de "sin Bearer": aqui hay un usuario detras y no sabemos cual. Caer al tenant fijo
    // escribiria sus hechos de negocio en la empresa equivocada, en silencio -- mismo criterio de
    // rechazo que cuando el token no trae org_id.
    [Fact]
    public async Task DerivarIdentidad_RechazaLaToolCall_CuandoElBearerNoSeValida()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueRechaza(),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException("no deberia invocarse con un token no validado")),
            new LoggerFalso<IdentidadTenantMcpMiddleware>());

        var act = async () => await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-no-validable",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IdentidadTenantMcpMiddleware.Mensajes.TokenNoValidado}*");
    }

    // Sin identidad derivada el middleware no puebla el ambiente, y el propagador cae al tenant fijo
    // interino de ConfiguracionIdentidadTenant (llamada directa con system key: smoke, local).
    [Fact]
    public async Task DerivarIdentidad_NoDerivaNinguna_CuandoNoHayBearer()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenException("no deberia invocarse sin Bearer")),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException("no deberia invocarse sin Bearer")),
            new LoggerFalso<IdentidadTenantMcpMiddleware>());

        var identidad = await middleware.DerivarIdentidadAsync(null, TestContext.Current.CancellationToken);

        identidad.Should().BeNull();
    }

    private static ClaimsPrincipal PrincipalConClaims(params Claim[] claims) => new(new ClaimsIdentity(claims));

    private static IdentidadTenantMcpMiddleware CrearMiddleware(
        ClaimsPrincipal principal, LoggerFalso<IdentidadTenantMcpMiddleware> logger) =>
        new(ValidadorTokenFalso.QueAutoriza(principal),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(new IdentidadTenant("org", "usuario", "membership")),
            logger);

    [Fact]
    public async Task DerivarIdentidad_NoRegistraNada_CuandoElBearerEsValido()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        var middleware = CrearMiddleware(PrincipalConClaims(
            new Claim("sub", "centinela-sub-7f3a"),
            new Claim("org_id", "centinela-org-9c1d"),
            new Claim("sid", "centinela-sid-5d4c"),
            new Claim("iss", "https://auth.ejemplo.test/authorize"),
            new Claim("iat", "1700000000", ClaimValueTypes.Integer64),
            new Claim("exp", "1700003600", ClaimValueTypes.Integer64)), logger);

        await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido", TestContext.Current.CancellationToken);

        logger.Entradas.Should().BeEmpty();
    }

    [Fact]
    public async Task DerivarIdentidad_NoEmiteLinea_CuandoNoHayBearer()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        var middleware = CrearMiddleware(PrincipalDeEjemplo, logger);

        var identidad = await middleware.DerivarIdentidadAsync(null, TestContext.Current.CancellationToken);

        identidad.Should().BeNull();
        logger.Entradas.Should().BeEmpty();
    }

    [Fact]
    public async Task DerivarIdentidad_NoEmiteLinea_CuandoElValidadorRechazaElBearer()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueRechaza(), DerivadorIdentidadTenantMcpFalso.QueDeriva(new IdentidadTenant("o", "u", "m")), logger);

        var act = async () => await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-no-validable", TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IdentidadTenantMcpMiddleware.Mensajes.TokenNoValidado}*");
        logger.Entradas.Should().BeEmpty();
    }
}
