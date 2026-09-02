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
        var identidadEsperada = new IdentidadTenant("org_acme", "usuario_123");
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
