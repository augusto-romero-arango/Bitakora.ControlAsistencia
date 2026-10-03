using System.Security.Claims;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public sealed record SesionUsuario(string? Correo, string OrganizacionId, string? OrganizacionNombre, string MembershipId)
{
    public const string ClaveItems = "Bitakora.SesionUsuario";

    public static SesionUsuario? Desde(ClaimsPrincipal principal) => throw new NotImplementedException();
}
