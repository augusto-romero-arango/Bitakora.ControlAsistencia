namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Identidad interina que el servidor envia en cada request saliente: hoy un tenant fijo de
/// operacion leido de app settings, no el usuario real conectado (issue de seguimiento sobre
/// autenticacion por cliente MCP). Forward-compatible con la etapa (b) de tenancy (MEF-ADR-0028
/// seccion 4).
/// </summary>
public sealed record IdentidadTenant(string TenantId, string UserId);
