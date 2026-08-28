namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction;

// Issue #456: comando para registrar una sede. Trigger: HTTP POST, Route: sedes.
// Ciudad/Direccion son opcionales e informativas (decision de sesion 2026-08-27): sin VO, sin
// regla de negocio propia en este issue.
public record RegistrarSede(string Codigo, string Nombre, string? Ciudad, string? Direccion);
