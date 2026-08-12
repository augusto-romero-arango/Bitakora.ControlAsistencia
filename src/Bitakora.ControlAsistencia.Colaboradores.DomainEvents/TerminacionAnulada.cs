namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que anula la terminacion registrada de la ULTIMA vinculacion de un
/// colaborador, reabriendola con su codigo y fecha de inicio originales intactos. Se persiste en
/// el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #354: sin payload -- el hecho es el evento mismo. No hay dato adicional que registrar: la
/// fecha efectiva de la terminacion anulada sigue integra en el VinculacionTerminada que el stream
/// ya conserva -- anular no borra el historial, solo dice "ese hecho ya no aplica".
/// Reemplaza al comando "CorregirFechaTerminacionVinculacion" que el desglose original planeaba
/// (decision de refinamiento 2026-08-11): corregir una fecha de terminacion es anular + terminar
/// de nuevo, dos intenciones que componen (ver ColaboradorAggregateRoot.AnularTerminacion).
/// No necesita ConfigurarSerializacion (sin campos que mapear) ni marca de bus
/// (IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores (issue #354 "Consumidores:
/// ninguno").
/// </remarks>
public sealed record TerminacionAnulada;
