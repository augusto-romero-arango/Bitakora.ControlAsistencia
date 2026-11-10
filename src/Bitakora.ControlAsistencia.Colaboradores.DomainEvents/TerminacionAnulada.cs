namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Anula la terminacion registrada de la ULTIMA vinculacion de un colaborador y la reabre con su
/// codigo y fecha de inicio originales.
/// </summary>
/// <remarks>
/// Sin payload: la fecha de la terminacion anulada sigue en el VinculacionTerminada del stream.
/// Corregir una fecha de terminacion es anular + terminar de nuevo; no hay evento de correccion.
/// </remarks>
public sealed record TerminacionAnulada;
