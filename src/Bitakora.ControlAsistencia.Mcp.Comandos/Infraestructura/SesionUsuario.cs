using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

internal sealed record SesionUsuario(string OrganizacionId, string MembershipId, string? SesionId = null)
{
    internal const string ClaveItems = "Bitakora.SesionUsuario";
    internal const string ClaimSesion = "sid";

    internal static SesionUsuario? Desde(ClaimsPrincipal principal) => throw new NotImplementedException();
}
