namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Sede de la VINCULACION vigente de un colaborador, no de la persona: un reingreso nace sin sede.
/// </summary>
/// <remarks>
/// Payload deliberadamente reducido al codigo -- referencia pura al maestro de Sedes: sin nombre ni
/// centro de costos, para que un rename o un cambio de CC de la sede no toque streams de
/// colaborador. Islas (CA-ADR-0029): prohibido referenciar Sedes.DomainEvents.
/// Representa siempre el reemplazo completo de la sede -- primera asignacion y reasignacion emiten
/// este mismo evento, y no existe evento de retiro.
/// </remarks>
public sealed record SedeAsignada(string CodigoSede);
