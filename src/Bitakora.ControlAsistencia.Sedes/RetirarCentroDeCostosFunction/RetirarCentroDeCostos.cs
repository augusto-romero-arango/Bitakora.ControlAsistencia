namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;

// Comando interno: el endpoint lo compone integramente desde el {codigo} de la ruta, sin body
// (MEF-ADR-0043 paso 3).
public record RetirarCentroDeCostos(string Codigo);
