namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction;

// Comando interno: el endpoint lo compone integramente desde la ruta, sin body.
public record RetirarDispositivo(string Codigo, string DispositivoId);
