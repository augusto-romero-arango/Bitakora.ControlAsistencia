using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

public interface IDerivadorIdentidadTenantMcp
{
    IdentidadTenant Derivar(ClaimsPrincipal principal);
}

public sealed partial class DerivadorIdentidadTenantMcp : IDerivadorIdentidadTenantMcp
{
    internal const string ClaimOrganizacion = "org_id";
    internal const string ClaimUsuario = "sub";

    public IdentidadTenant Derivar(ClaimsPrincipal principal) => throw new NotImplementedException();
}
