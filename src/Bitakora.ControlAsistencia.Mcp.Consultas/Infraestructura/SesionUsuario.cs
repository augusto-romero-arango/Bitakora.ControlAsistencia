using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public sealed record SesionUsuario(string? Correo, string OrganizacionId, string? OrganizacionNombre, string MembershipId)
{
    public const string ClaveItems = "Bitakora.SesionUsuario";
    internal const string ClaimCorreo = "user_email";
    internal const string ClaimNombreOrganizacion = "organization_name";

    public static SesionUsuario? Desde(ClaimsPrincipal principal)
    {
        var organizacion = principal.FindFirstValue(DerivadorIdentidadTenantMcp.ClaimOrganizacion);
        var membership = principal.FindFirstValue(DerivadorIdentidadTenantMcp.ClaimOrganizationMembership);
        if (string.IsNullOrWhiteSpace(organizacion) || string.IsNullOrWhiteSpace(membership))
            return null;

        return new SesionUsuario(
            Vacio(principal.FindFirstValue(ClaimCorreo)),
            organizacion,
            Vacio(principal.FindFirstValue(ClaimNombreOrganizacion)),
            membership);
    }

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}
