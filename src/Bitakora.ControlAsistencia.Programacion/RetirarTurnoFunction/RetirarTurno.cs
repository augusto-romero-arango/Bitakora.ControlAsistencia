namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction;

// Comando interno: el endpoint lo compone integramente desde el {id} de la ruta, sin body
// (MEF-ADR-0043 paso 3 -- remocion veraz de un sub-recurso direccionable).
public record RetirarTurno(Guid TurnoId);
