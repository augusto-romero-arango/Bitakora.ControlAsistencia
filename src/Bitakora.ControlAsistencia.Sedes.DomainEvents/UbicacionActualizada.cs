namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #457 (MEF-ADR-0043 paso 2): reemplazo completo de la ubicacion (Ciudad+Direccion) de una
// sede existente -- valor atomico, ambos campos opcionales e informativos (decision de sesion
// 2026-08-27), sin impacto en calculos.
public record UbicacionActualizada(string? Ciudad, string? Direccion);
