using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

internal sealed record SesionUsuario(string? Correo, string OrganizacionId, string? OrganizacionNombre, string MembershipId, string? SesionId = null)
{
    internal const string ClaveItems = "Bitakora.SesionUsuario";
    internal const string ClaimSesion = "sid";
    internal const string ClaimCorreo = "user_email";
    internal const string ClaimNombreOrganizacion = "organization_name";

    internal static SesionUsuario? Desde(ClaimsPrincipal principal)
    {
        var organizacion = principal.FindFirstValue(DerivadorIdentidadTenantMcp.ClaimOrganizacion);
        var membership = principal.FindFirstValue(DerivadorIdentidadTenantMcp.ClaimOrganizationMembership);
        if (string.IsNullOrWhiteSpace(organizacion) || string.IsNullOrWhiteSpace(membership))
            return null;

        return new SesionUsuario(
            Vacio(principal.FindFirstValue(ClaimCorreo)),
            organizacion,
            Vacio(principal.FindFirstValue(ClaimNombreOrganizacion)),
            membership,
            Vacio(principal.FindFirstValue(ClaimSesion)));
    }

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}
