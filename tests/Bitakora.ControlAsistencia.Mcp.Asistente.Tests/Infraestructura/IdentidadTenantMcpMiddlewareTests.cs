using System.Security.Claims;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CerrarSesion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerSesion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

// Nucleo testable de IdentidadTenantMcpMiddleware: DerivarIdentidadAsync opera sobre el encabezado
// Authorization ya extraido, porque FunctionContext/ToolInvocationContext no son instanciables en un
// unit test (MEF-ADR-0048 seccion 1). El validador y el derivador se fakean para aislar la
// orquestacion de la criptografia (ValidadorTokenAuthKitTests) y de la traduccion de claims
// (DerivadorIdentidadTenantMcpTests).
public class IdentidadTenantMcpMiddlewareTests
{
    private static readonly IdentidadTenant IdentidadDeEjemplo = new("org_acme", "usuario_123", "om_123");

    private static ClaimsPrincipal PrincipalDeEjemplo =>
        new(new ClaimsIdentity([new Claim("sub", "usuario_123")]));

    private static ClaimsPrincipal PrincipalConSesion => new(new ClaimsIdentity([
        new Claim("sub", "usuario_123"),
        new Claim("org_id", "org_acme"),
        new Claim("organization_membership_id", "om_123"),
        new Claim("user_email", "ana@acme.co"),
        new Claim("organization_name", "Acme SAS"),
        new Claim("sid", "sesion 01+A/B")
    ]));

    [Fact]
    public async Task DerivarAsync_RestauraSesionParaAmbasTools_CuandoElBearerTraeOrganizacionMembershipYSid()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueAutoriza(PrincipalConSesion),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(IdentidadDeEjemplo));

        var (identidad, sesion) = await middleware.DerivarAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido",
            TestContext.Current.CancellationToken);

        identidad.Should().Be(IdentidadDeEjemplo);
        sesion.Should().Be(new SesionUsuario("ana@acme.co", "org_acme", "Acme SAS", "om_123", "sesion 01+A/B"));

        using var obtener = JsonDocument.Parse(new ObtenerSesionTool(
            new IdentidadTenant("tenant-fijo", "usuario-fijo", "membership-fijo")).Describir(sesion));
        var respuesta = obtener.RootElement;
        respuesta.GetProperty("origen").GetString().Should().Be("sesion");
        respuesta.GetProperty("correo").GetString().Should().Be("ana@acme.co");
        respuesta.GetProperty("empresa").GetProperty("id").GetString().Should().Be("org_acme");
        respuesta.GetProperty("empresa").GetProperty("nombre").GetString().Should().Be("Acme SAS");
        respuesta.GetProperty("membership").GetString().Should().Be("om_123");
        respuesta.TryGetProperty("advertencia", out _).Should().BeFalse();

        using var cerrar = JsonDocument.Parse(new CerrarSesionTool().Describir(sesion));
        cerrar.RootElement.GetProperty("url").GetString().Should().Be(
            "https://api.workos.com/user_management/sessions/logout?session_id=sesion%2001%2BA%2FB");
    }

    [Fact]
    public async Task DerivarAsync_NoProduceSesion_CuandoNoHayBearer()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenException("no deberia invocarse sin Bearer")),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException("no deberia invocarse sin Bearer")));

        var (identidad, sesion) = await middleware.DerivarAsync(null, TestContext.Current.CancellationToken);

        identidad.Should().BeNull();
        sesion.Should().BeNull();
        using var obtener = JsonDocument.Parse(new ObtenerSesionTool(
            new IdentidadTenant("tenant-fijo", "usuario-fijo", "membership-fijo")).Describir(sesion));
        obtener.RootElement.GetProperty("origen").GetString().Should().Be("tenant_fijo");
        obtener.RootElement.GetProperty("empresa").GetProperty("id").GetString().Should().Be("tenant-fijo");
    }

    [Fact]
    public async Task DerivarAsync_RechazaLaToolCall_CuandoElBearerNoSeValida()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueRechaza(),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException("no deberia invocarse con un token no validado")));

        var act = async () => await middleware.DerivarAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-no-validable",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IdentidadTenantMcpMiddleware.Mensajes.TokenNoValidado}*");
    }

    [Fact]
    public async Task DerivarIdentidad_RetornaLaIdentidadDelToken_CuandoElBearerTraeOrganizacion()
    {
        var identidadEsperada = new IdentidadTenant("org_acme", "usuario_123", "membership-fijo");
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueAutoriza(PrincipalDeEjemplo),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(identidadEsperada));

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
                new InvalidOperationException(DerivadorIdentidadTenantMcp.Mensajes.OrganizacionAusente)));

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
                new InvalidOperationException("no deberia invocarse con un token no validado")));

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
                new InvalidOperationException("no deberia invocarse sin Bearer")));

        var identidad = await middleware.DerivarIdentidadAsync(null, TestContext.Current.CancellationToken);

        identidad.Should().BeNull();
    }
}
