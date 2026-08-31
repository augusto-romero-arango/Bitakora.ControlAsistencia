namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Identidad que el servidor estampa en cada request saliente: un tenant fijo de operacion, NO el
/// usuario real conectado. Sus valores deben coincidir con los de <c>TenantResolverFijo</c> de los
/// dominios (CA-ADR-0027, <c>*DEFAULT*</c>/<c>sin-identificar</c>): al pasar a la etapa (b) de
/// tenancy (MEF-ADR-0028 seccion 4) los Function Apps resuelven el tenant desde estos headers, y
/// un valor distinto consultaria un tenant sin ninguno de los datos ya persistidos.
/// </summary>
public sealed record IdentidadTenant(string TenantId, string UserId);
