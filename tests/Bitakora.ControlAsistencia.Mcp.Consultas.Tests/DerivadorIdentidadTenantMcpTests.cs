using System.Security.Claims;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// El ClaimsPrincipal llega ya validado (issuer+firma+expiracion) por IValidadorTokenAuthKit: este
// derivador no revalida nada, solo traduce org_id/sub.
public class DerivadorIdentidadTenantMcpTests
{
    private static ClaimsPrincipal PrincipalCon(string? orgId, string? sub)
    {
        var claims = new List<Claim>();
        if (orgId is not null)
            claims.Add(new Claim(DerivadorIdentidadTenantMcp.ClaimOrganizacion, orgId));
        if (sub is not null)
            claims.Add(new Claim(DerivadorIdentidadTenantMcp.ClaimUsuario, sub));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public void Derivar_RetornaLaIdentidadDelToken_CuandoTraeOrganizacionYUsuario()
    {
        var derivador = new DerivadorIdentidadTenantMcp();
        var principal = PrincipalCon("org_acme", "usuario_123");

        var identidad = derivador.Derivar(principal);

        identidad.Should().Be(new IdentidadTenant("org_acme", "usuario_123"));
    }

    [Fact]
    public void Derivar_LanzaInvalidOperationException_CuandoFaltaOrgId()
    {
        var derivador = new DerivadorIdentidadTenantMcp();
        var principal = PrincipalCon(null, "usuario_123");

        var act = () => derivador.Derivar(principal);

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{DerivadorIdentidadTenantMcp.Mensajes.OrganizacionAusente}*");
    }

    // Un token sin sub (por ejemplo uno de maquina a maquina) no identifica usuario: rechazo
    // explicito en vez de un X-User-Id vacio aguas abajo.
    [Fact]
    public void Derivar_LanzaInvalidOperationException_CuandoFaltaSub()
    {
        var derivador = new DerivadorIdentidadTenantMcp();
        var principal = PrincipalCon("org_acme", null);

        var act = () => derivador.Derivar(principal);

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{DerivadorIdentidadTenantMcp.Mensajes.UsuarioAusente}*");
    }
}
