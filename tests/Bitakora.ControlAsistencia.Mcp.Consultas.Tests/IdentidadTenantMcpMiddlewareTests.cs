using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

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

    private const string EmisorDelToken = "https://auth.ejemplo.test/authorize";

    private static ClaimsPrincipal PrincipalConClaims(params Claim[] claims) => new(new ClaimsIdentity(claims));

    private static IdentidadTenantMcpMiddleware CrearMiddleware(
        ClaimsPrincipal principal, LoggerFalso<IdentidadTenantMcpMiddleware> logger) =>
        new(ValidadorTokenFalso.QueAutoriza(principal),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(new IdentidadTenant("org", "usuario", "membership")),
            logger);

    private static object? Propiedad(LoggerFalso<IdentidadTenantMcpMiddleware>.Entrada entrada, string nombre) =>
        entrada.Estado.GetValueOrDefault(nombre);

    [Fact]
    public async Task DerivarIdentidad_EmiteUnaLineaConLaFormaDelToken_CuandoElBearerEsValido()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        var middleware = CrearMiddleware(PrincipalConClaims(
            new Claim("sub", "valor-centinela-sub"),
            new Claim("org_id", "valor-centinela-org"),
            new Claim("sid", "valor-centinela-sid"),
            new Claim("iss", EmisorDelToken),
            new Claim("iat", "1700000000", ClaimValueTypes.Integer64),
            new Claim("exp", "1700003600", ClaimValueTypes.Integer64)), logger);

        await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido", TestContext.Current.CancellationToken);

        var entrada = logger.Entradas.Should().ContainSingle().Subject;
        foreach (var nombre in new[] { "sub", "org_id", "sid", "iss", "iat", "exp" })
            entrada.Mensaje.Should().Contain(nombre);
        Propiedad(entrada, "TieneSid").Should().Be(true);
        Propiedad(entrada, "Emisor").Should().Be(EmisorDelToken);
        Convert.ToInt64(Propiedad(entrada, "DuracionTokenSegundos")).Should().Be(3600);
        entrada.Mensaje.Should().Contain("3600");
    }

    [Fact]
    public async Task DerivarIdentidad_ReportaSinSidYSinDuracion_CuandoElTokenNoTraeSidNiExpIat()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        var middleware = CrearMiddleware(PrincipalConClaims(
            new Claim("sub", "valor-centinela-sub"),
            new Claim("iss", EmisorDelToken),
            new Claim("exp", "1700003600", ClaimValueTypes.Integer64)), logger);

        await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido", TestContext.Current.CancellationToken);

        var entrada = logger.Entradas.Should().ContainSingle().Subject;
        Propiedad(entrada, "TieneSid").Should().Be(false);
        Propiedad(entrada, "DuracionTokenSegundos").Should().BeNull();
    }

    [Fact]
    public async Task DerivarIdentidad_NoRegistraValoresDeClaims_CuandoElBearerEsValido()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        string[] centinelas =
        [
            "centinela-sub-7f3a", "centinela-org-9c1d", "centinela-membership-2b8e",
            "centinela-sid-5d4c", "centinela-email-1a6f@ejemplo.test"
        ];
        var middleware = CrearMiddleware(PrincipalConClaims(
            new Claim("sub", centinelas[0]),
            new Claim("org_id", centinelas[1]),
            new Claim("organization_membership_id", centinelas[2]),
            new Claim("sid", centinelas[3]),
            new Claim("email", centinelas[4]),
            new Claim("iss", EmisorDelToken),
            new Claim("iat", "1700000000", ClaimValueTypes.Integer64),
            new Claim("exp", "1700003600", ClaimValueTypes.Integer64)), logger);

        await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido", TestContext.Current.CancellationToken);

        var entrada = logger.Entradas.Should().ContainSingle().Subject;
        var registrado = entrada.Mensaje + " " + string.Join(" ", entrada.Estado.Select(p =>
            p.Value is System.Collections.IEnumerable lista and not string
                ? string.Join(" ", lista.Cast<object?>())
                : p.Value?.ToString()));
        foreach (var centinela in centinelas)
            registrado.Should().NotContain(centinela);
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
    public async Task DerivarIdentidad_NoEmiteLinea_CuandoElValidadorLanzaPorBearerInvalido()
    {
        var logger = new LoggerFalso<IdentidadTenantMcpMiddleware>();
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenException("firma invalida")),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(new IdentidadTenant("o", "u", "m")), logger);

        var act = async () => await middleware.DerivarIdentidadAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-no-validable", TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<SecurityTokenException>().WithMessage("*firma invalida*");
        logger.Entradas.Should().BeEmpty();
    }
}
