using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

internal sealed record SesionUsuario(
    string? Correo, string OrganizacionId, string? OrganizacionNombre, string MembershipId, string? SesionId = null)
{
    internal const string ClaveItems = "Bitakora.SesionUsuario";
    internal const string ClaimSesion = "sid";
    internal const string ClaimCorreo = "user_email";
    internal const string ClaimNombreOrganizacion = "organization_name";

    internal static SesionUsuario? Desde(ClaimsPrincipal principal) => throw new NotImplementedException();
}
