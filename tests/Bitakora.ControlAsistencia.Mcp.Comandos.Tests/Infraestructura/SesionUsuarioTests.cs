using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Infraestructura;

public class SesionUsuarioTests
{
    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Tipo, c.Valor))));

    [Fact]
    public void SesionUsuario_LeeOrganizacionMembershipYSid_CuandoElPrincipalTraeTodosLosClaims()
    {
        var sesion = SesionUsuario.Desde(Principal(
            ("org_id", "org_acme"), ("organization_membership_id", "om_123"), ("sid", "session_01ABC")));

        sesion.Should().Be(new SesionUsuario("org_acme", "om_123", "session_01ABC"));
    }

    [Fact]
    public void SesionUsuario_DejaNuloElSid_CuandoElPrincipalNoTraeElClaimSid()
    {
        var sesion = SesionUsuario.Desde(Principal(
            ("org_id", "org_acme"), ("organization_membership_id", "om_123")));

        sesion!.SesionId.Should().BeNull();
    }

    [Fact]
    public void SesionUsuario_NoHaySesion_CuandoFaltaLaOrganizacionOElMembership()
    {
        SesionUsuario.Desde(Principal(("sid", "session_01ABC"))).Should().BeNull();
    }
}
