using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public interface IDerivadorIdentidadTenantMcp
{
    IdentidadTenant Derivar(ClaimsPrincipal principal);
}

// Traduce el ClaimsPrincipal que IValidadorTokenAuthKit ya valido (issuer+firma+expiracion,
// issue #554) a la identidad de tenant real del usuario MCP (issue #540): org_id (claim de
// organizacion de WorkOS Connect, elegida por el usuario al autorizar -- workos.com/docs/authkit/
// connect/oauth, "Organization Access") -> TenantId; sub -> UserId. CA-2: sin org_id el usuario no
// pertenece a ninguna organizacion -- rechazo explicito con mensaje .resx, nunca un fallback
// silencioso al tenant fijo de ConfiguracionIdentidadTenant ni a un tenant vacio.
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
        return new IdentidadTenant(organizacion, usuario!);
    }
}
