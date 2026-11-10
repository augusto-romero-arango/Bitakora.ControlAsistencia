namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Bloque de tiempo absoluto (hora local del tenant) que resulta de segmentar un turno diario
/// (<see cref="TurnoDiario.Segmentar"/>): sin solape, roto en cada medianoche que cruce. Forma plana
/// para clientes de calendario.
/// </summary>
/// <remarks>
/// No es payload de un evento: no se persiste en <c>mt_events</c> ni requiere
/// <c>ConfigurarSerializacion</c>.
/// Descansos y extras heredan la sede de su franja madre; Sede es null cuando la franja no tiene
/// sede asignada (turno prearmado multi-sede sin resolver).
/// </remarks>
public record BloqueTurno(TipoBloque Tipo, DateTime Inicio, DateTime Fin, SedeProgramada? Sede = null);
