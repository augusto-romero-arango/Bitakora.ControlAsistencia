namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// Issue #457 (MEF-ADR-0043 paso 2): comando interno para reemplazar completa la ubicacion
// (Ciudad+Direccion) de una sede existente -- valor atomico direccionable por {codigo}. Trigger
// HTTP PUT, Route: sedes/{codigo}/ubicacion. Ciudad/Direccion son opcionales e informativas
// (decision de sesion 2026-08-27): sin VO, sin regla de negocio propia en este issue.
public record ActualizarUbicacionSede(string Codigo, string? Ciudad, string? Direccion);
