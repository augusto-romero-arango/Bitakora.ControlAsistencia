using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

public interface IDerivadorIdentidadTenantMcp
{
    IdentidadTenant Derivar(ClaimsPrincipal principal);
}

// Traduce a IdentidadTenant el ClaimsPrincipal que IValidadorTokenAuthKit ya valido: org_id (la
// organizacion que el usuario elige al autorizar en WorkOS Connect -- workos.com/docs/authkit/
// connect/oauth, "Organization Access") -> TenantId; sub -> UserId. Ninguno de los dos admite
// fallback: sin organizacion o sin usuario no hay tenant que derivar, y caer al tenant fijo de
// ConfiguracionIdentidadTenant daria acceso a datos de otra empresa.
public sealed partial class DerivadorIdentidadTenantMcp : IDerivadorIdentidadTenantMcp
{
    internal const string ClaimOrganizacion = "org_id";
    internal const string ClaimUsuario = "sub";

    public IdentidadTenant Derivar(ClaimsPrincipal principal)
    {
        var organizacion = principal.FindFirstValue(ClaimOrganizacion);
        if (string.IsNullOrWhiteSpace(organizacion))
            throw new InvalidOperationException(Mensajes.OrganizacionAusente);

        var usuario = principal.FindFirstValue(ClaimUsuario);
        if (string.IsNullOrWhiteSpace(usuario))
            throw new InvalidOperationException(Mensajes.UsuarioAusente);

        return new IdentidadTenant(organizacion, usuario);
    }
}
