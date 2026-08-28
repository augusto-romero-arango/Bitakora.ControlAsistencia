namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// Comando interno: el endpoint lo compone desde el {codigo} de la ruta mas el body.
public record InstalarDispositivo(string Codigo, string DispositivoId);
