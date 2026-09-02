namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Identidad propagada a las Function Apps del BC en cada request saliente (MEF-ADR-0047 decision
/// 6). La derivada del token del usuario autenticado la produce IdentidadTenantMcpMiddleware; la
/// registrada en el contenedor es el valor fijo por despliegue con el que el propagador responde a
/// una invocacion sin Bearer (issue #572).
/// </summary>
public sealed record IdentidadTenant(string TenantId, string UserId);
