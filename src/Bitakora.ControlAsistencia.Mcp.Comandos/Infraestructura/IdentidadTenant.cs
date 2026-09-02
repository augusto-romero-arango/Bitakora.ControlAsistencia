namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Identidad propagada a las Function Apps del BC en cada request saliente (MEF-ADR-0047 decision
/// 6). Interina mientras el servidor no reciba Authorization de una tool call (decision 7): un
/// valor fijo por despliegue, nunca derivado del cliente MCP conectado.
/// </summary>
public sealed record IdentidadTenant(string TenantId, string UserId);
