namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Bloque de tiempo absoluto (hora local del tenant) que compone el turno vigente -- forma propia
/// de ReadModels, homonima deliberada de <c>BloqueTurno</c> (ControlHoras.DomainEvents, issue #327)
/// para que la proyeccion no cargue un alias de tipo entre ambos (issue #328, "Investigacion del
/// planner"). Record plano, sin comportamiento: la clase de proyeccion companion es quien mapea
/// cada <c>BloqueTurno</c> que produce <c>TurnoDiario.Segmentar</c> a este tipo.
/// </summary>
public sealed record Bloque(TipoBloque Tipo, DateTime Inicio, DateTime Fin);
