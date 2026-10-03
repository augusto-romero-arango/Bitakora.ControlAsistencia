using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class SesionUsuarioTests
{
    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Tipo, c.Valor))));

    [Fact]
    public void SesionUsuario_LeeCorreoEmpresaNombreYMembership_CuandoElPrincipalTraeTodosLosClaims()
    {
        var sesion = SesionUsuario.Desde(Principal(
            ("user_email", "ana@acme.co"), ("org_id", "org_acme"),
            ("organization_name", "Acme SAS"), ("organization_membership_id", "om_123")));

        sesion.Should().Be(new SesionUsuario("ana@acme.co", "org_acme", "Acme SAS", "om_123"));
    }

    [Fact]
    public void SesionUsuario_DejaNulosLosOpcionales_CuandoFaltanCorreoYNombreDeEmpresa()
    {
        var sesion = SesionUsuario.Desde(Principal(
            ("org_id", "org_acme"), ("organization_membership_id", "om_123")));

        sesion.Should().Be(new SesionUsuario(null, "org_acme", null, "om_123"));
    }

    [Fact]
    public void SesionUsuario_LeeElSid_CuandoElPrincipalTraeElClaimSid()
    {
        var sesion = SesionUsuario.Desde(Principal(
            ("org_id", "org_acme"), ("organization_membership_id", "om_123"), ("sid", "session_01ABC")));

        sesion.Should().Be(new SesionUsuario(null, "org_acme", null, "om_123", "session_01ABC"));
    }

    [Fact]
    public void SesionUsuario_DejaNuloElSid_CuandoElPrincipalNoTraeElClaimSid()
    {
        var sesion = SesionUsuario.Desde(Principal(
            ("org_id", "org_acme"), ("organization_membership_id", "om_123")));

        sesion!.SesionId.Should().BeNull();
    }
}
