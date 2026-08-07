namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Bloque de tiempo absoluto (hora local del tenant) que compone el turno vigente -- forma propia
/// de ReadModels, homonima deliberada de <c>BloqueTurno</c> (ControlHoras.DomainEvents, issue #327)
/// para que la proyeccion no cargue un alias de tipo entre ambos (issue #328, "Investigacion del
/// planner"). Record plano, sin comportamiento: la clase de proyeccion companion es quien mapea
/// cada <c>BloqueTurno</c> que produce <c>TurnoDiario.Segmentar</c> a este tipo.
/// </summary>
/// <remarks>
/// Issue #337: SedeId/NombreSede son campos aditivos y opcionales (strings anulables, planos --
/// sin record <c>Sede</c> anidado: el nombre puro sigue reservado al concepto rico del futuro
/// maestro de sedes, #338). Espejo de <c>BloqueTurno.Sede</c> (ControlHoras.DomainEvents, issue
/// #336) desagregado en sus dos campos primitivos -- la sede va POR BLOQUE, nunca a nivel de dia
/// (un dia multi-sede no tiene "una" sede). Ambos quedan null cuando el bloque proviene de una
/// franja sin sede asignada (turno prearmado sin resolver) o de un evento anterior a #336 que
/// nunca trajo el campo.
/// </remarks>
public sealed record Bloque(
    TipoBloque Tipo,
    DateTime Inicio,
    DateTime Fin,
    string? SedeId = null,
    string? NombreSede = null);
