namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// Comando interno: el endpoint lo compone desde el {codigo} de la ruta mas el body.
public record ActualizarUbicacionSede(string Codigo, string? Ciudad, string? Direccion);
