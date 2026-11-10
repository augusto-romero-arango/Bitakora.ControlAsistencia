namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Cierre de la vinculacion vigente de un colaborador.
/// </summary>
/// <remarks>
/// FechaEfectiva puede ser pasada (registro tardio) o futura (preaviso). Sin motivo: ninguna regla
/// ni vista de este BC lo consume; el "por que" es de RRHH/nomina.
/// </remarks>
public sealed record VinculacionTerminada(DateOnly FechaEfectiva);
