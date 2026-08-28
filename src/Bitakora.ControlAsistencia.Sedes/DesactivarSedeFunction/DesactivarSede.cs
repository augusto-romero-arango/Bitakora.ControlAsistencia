namespace Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction;

// Comando interno: el endpoint lo compone integramente desde el {codigo} de la ruta, sin body
// (MEF-ADR-0043 paso 4).
public record DesactivarSede(string Codigo);
