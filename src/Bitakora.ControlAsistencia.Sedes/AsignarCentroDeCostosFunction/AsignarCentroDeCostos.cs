namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;

// Comando interno: el endpoint lo compone desde el {codigo} de la ruta mas el body.
public record AsignarCentroDeCostos(string Codigo, string CentroDeCostos);
