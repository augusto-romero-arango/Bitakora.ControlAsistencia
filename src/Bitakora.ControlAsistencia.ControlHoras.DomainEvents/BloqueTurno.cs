namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Bloque de tiempo absoluto (hora local del tenant) que resulta de segmentar un turno diario
/// (<see cref="TurnoDiario.Segmentar"/>, issue #327). Forma plana pensada para consumo directo de
/// clientes de calendario (FullCalendar, Syncfusion Scheduler): sin solape, roto en cada medianoche
/// que cruce.
/// </summary>
/// <remarks>
/// No se persiste en <c>mt_events</c> ni requiere <c>ConfigurarSerializacion</c>: es el resultado de
/// un metodo de lectura pura, no el payload de un evento. Su serializacion como parte del documento
/// del read model es alcance del issue sucesor (TurnoVigente).
///
/// Issue #336: Sede es campo aditivo y opcional -- el bloque conoce donde rige. Los bloques de
/// descanso y extra heredan la sede de la franja madre que los contiene (no tienen sede propia);
/// una franja sin sede asignada (turno prearmado multi-sede sin resolver) produce bloques con
/// sede null. La plomeria que estampa el valor vive en <see cref="TurnoDiario.Segmentar"/> y su
/// cadena de recorte (MEF-ADR-0012, Tell-don't-Ask).
/// </remarks>
public record BloqueTurno(TipoBloque Tipo, DateTime Inicio, DateTime Fin, SedeProgramada? Sede = null);
