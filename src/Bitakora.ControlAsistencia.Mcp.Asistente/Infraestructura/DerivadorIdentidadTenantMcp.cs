using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public interface IDerivadorIdentidadTenantMcp
{
    IdentidadTenant Derivar(ClaimsPrincipal principal);
}

public sealed partial class DerivadorIdentidadTenantMcp : IDerivadorIdentidadTenantMcp
{
    internal const string ClaimOrganizacion = "org_id";
    internal const string ClaimUsuario = "sub";
    internal const string ClaimOrganizationMembership = "organization_membership_id";

    public IdentidadTenant Derivar(ClaimsPrincipal principal) => throw new NotImplementedException();
}
