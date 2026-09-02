using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;
using Bitakora.ControlAsistencia.TenantResolver;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-1/CA-2/CA-3 (issue #540): nucleo testable de IdentidadTenantMcpMiddleware
// (PoblarIdentidadAmbienteAsync opera sobre el encabezado Authorization ya extraido, nunca sobre
// FunctionContext/ToolInvocationContext -- inalcanzables en un unit test, mismo limite que
// MEF-ADR-0048 seccion 1 documenta para tools/list). El validador y el derivador se fakean para
// aislar la orquestacion del middleware de su criptografia (ValidadorTokenAuthKitTests) y de su
// traduccion de claims (DerivadorIdentidadTenantMcpTests).
public class IdentidadTenantMcpMiddlewareTests
{
    private static ClaimsPrincipal PrincipalDeEjemplo =>
        new(new ClaimsIdentity([new Claim("sub", "usuario_123")]));

    [Fact]
    public async Task PoblarIdentidadAmbiente_EstableceLaIdentidadDelToken_CuandoElBearerTraeOrganizacion()
    {
        var identidadEsperada = new IdentidadTenant("org_acme", "usuario_123");
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueAutoriza(PrincipalDeEjemplo),
            DerivadorIdentidadTenantMcpFalso.QueDeriva(identidadEsperada));

        await middleware.PoblarIdentidadAmbienteAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-valido",
            TestContext.Current.CancellationToken);

        TenantExecutionContext.TryObtener(out var tenantId, out var userId).Should().BeTrue();
        tenantId.Should().Be("org_acme");
        userId.Should().Be("usuario_123");
    }

    [Fact]
    public async Task PoblarIdentidadAmbiente_PropagaElRechazoDelDerivador_CuandoElUsuarioNoTieneOrganizacion()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueAutoriza(PrincipalDeEjemplo),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException(DerivadorIdentidadTenantMcp.Mensajes.OrganizacionAusente)));

        var act = async () => await middleware.PoblarIdentidadAmbienteAsync(
            $"{IdentidadTenantMcpMiddleware.EsquemaBearer}token-sin-org",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{DerivadorIdentidadTenantMcp.Mensajes.OrganizacionAusente}*");
    }

    [Fact]
    public async Task PoblarIdentidadAmbiente_NoTocaElAmbiente_CuandoNoHayBearer()
    {
        var middleware = new IdentidadTenantMcpMiddleware(
            ValidadorTokenFalso.QueFalla(new SecurityTokenException("no deberia invocarse sin Bearer")),
            DerivadorIdentidadTenantMcpFalso.QueFalla(
                new InvalidOperationException("no deberia invocarse sin Bearer")));

        await middleware.PoblarIdentidadAmbienteAsync(null, TestContext.Current.CancellationToken);

        TenantExecutionContext.TryObtener(out _, out _).Should().BeFalse();
    }
}
