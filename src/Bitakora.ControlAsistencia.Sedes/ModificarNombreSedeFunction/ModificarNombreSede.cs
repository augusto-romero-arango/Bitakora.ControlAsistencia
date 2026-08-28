namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

// Comando interno: el endpoint lo compone desde el {codigo} de la ruta mas el body.
public record ModificarNombreSede(string Codigo, string Nombre);
