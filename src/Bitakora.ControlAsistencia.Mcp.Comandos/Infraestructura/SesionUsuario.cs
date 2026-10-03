using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

internal sealed record SesionUsuario(string OrganizacionId, string MembershipId, string? SesionId = null)
{
    internal const string ClaveItems = "Bitakora.SesionUsuario";
    internal const string ClaimSesion = "sid";

    internal static SesionUsuario? Desde(ClaimsPrincipal principal)
    {
        var organizacion = principal.FindFirstValue(DerivadorIdentidadTenantMcp.ClaimOrganizacion);
        var membership = principal.FindFirstValue(DerivadorIdentidadTenantMcp.ClaimOrganizationMembership);
        if (string.IsNullOrWhiteSpace(organizacion) || string.IsNullOrWhiteSpace(membership))
            return null;

        var sid = principal.FindFirstValue(ClaimSesion);
        return new SesionUsuario(organizacion, membership, string.IsNullOrWhiteSpace(sid) ? null : sid);
    }
}
