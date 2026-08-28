namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;

// Comando interno: el endpoint lo compone desde el {codigo} de la ruta mas el body. CC opaco (issue
// #458): ningun parseo tipado, ninguna normalizacion -- se estampa tal cual llega.
public record AsignarCentroDeCostos(string Codigo, string CentroDeCostos);
