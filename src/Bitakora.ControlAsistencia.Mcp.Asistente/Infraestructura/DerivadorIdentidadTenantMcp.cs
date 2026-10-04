using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public interface IDerivadorIdentidadTenantMcp
{
    IdentidadTenant Derivar(ClaimsPrincipal principal);
}

// Traduce a IdentidadTenant el ClaimsPrincipal que IValidadorTokenAuthKit ya valido: org_id (la
// organizacion que el usuario elige al autorizar en WorkOS Connect -- workos.com/docs/authkit/
// connect/oauth, "Organization Access") -> TenantId; sub -> UserId; organization_membership_id ->
// OrganizationMembershipId (CA-ADR-0032). Ninguno admite fallback: sin cualquiera de los tres no
// hay identidad que derivar, y caer al tenant fijo de ConfiguracionIdentidadTenant daria acceso a
// datos de otra empresa.
public sealed partial class DerivadorIdentidadTenantMcp : IDerivadorIdentidadTenantMcp
{
    internal const string ClaimOrganizacion = "org_id";
    internal const string ClaimUsuario = "sub";
    internal const string ClaimOrganizationMembership = "organization_membership_id";

    public IdentidadTenant Derivar(ClaimsPrincipal principal)
    {
        var organizacion = principal.FindFirstValue(ClaimOrganizacion);
        if (string.IsNullOrWhiteSpace(organizacion))
            throw new InvalidOperationException(Mensajes.OrganizacionAusente);

        var usuario = principal.FindFirstValue(ClaimUsuario);
        if (string.IsNullOrWhiteSpace(usuario))
            throw new InvalidOperationException(Mensajes.UsuarioAusente);

        var membership = principal.FindFirstValue(ClaimOrganizationMembership);
        if (string.IsNullOrWhiteSpace(membership))
            throw new InvalidOperationException(Mensajes.OrganizationMembershipAusente);

        return new IdentidadTenant(organizacion, usuario, membership);
    }
}
