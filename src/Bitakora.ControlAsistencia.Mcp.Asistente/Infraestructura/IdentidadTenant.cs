namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Identidad propagada a las Function Apps del BC en cada request saliente. La derivada del token la
/// produce IdentidadTenantMcpMiddleware; la registrada en el contenedor es el valor fijo con el que el
/// propagador responde a una invocacion sin Bearer.
/// </summary>
public sealed record IdentidadTenant(string TenantId, string UserId, string OrganizationMembershipId);
