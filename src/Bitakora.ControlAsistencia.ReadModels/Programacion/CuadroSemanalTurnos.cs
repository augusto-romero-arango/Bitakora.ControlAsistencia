namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Cuadro semanal de turnos de una plantilla: grilla dias x turnos que el Programador usa para
/// saber si la plantilla que necesita ya existe, elegir cual usar y ver de un vistazo que turno
/// toca cada dia (issue #624).
/// </summary>
/// <remarks>
/// Record plano SIN partial ni comportamiento (MEF-ADR-0035): el mapeo evento -> vista vive
/// integro en la clase companion CuadroSemanalTurnosProjection, en el worker.
///
/// Vive en ReadModels, la cuarta isla del repo -- cero ProjectReference (MEF-ADR-0041 decision 2):
/// DiaDelCuadro es un tipo propio, nunca uno de Programacion.DomainEvents. Por eso Dia es int (no
/// DiaSemana) y TurnoId es string (no Guid).
///
/// Id es el stream key de la plantilla (PlantillaId.ToString(), StreamIdentity.AsString), tomado
/// de IEvent.StreamKey, nunca recomputado del payload (criterio de FichaTurno).
///
/// Solo guarda lo que sale del stream de la plantilla: NombreTurno, Descripcion, TurnoRetirado y
/// Completa se derivan en #625 al juntar con FichaTurno (decision del refinamiento 2026-09-05,
/// N1 + composicion en la lectura, enmienda de CA-ADR-0034 decision 5).
/// </remarks>
public sealed record CuadroSemanalTurnos(
    string Id,
    string Nombre,
    int Semanas,
    IReadOnlyList<DiaDelCuadro> Dias);

/// <summary>
/// Un dia asignado del cuadro: la referencia viva al turno (TurnoId, CA-ADR-0034 decision 2), nunca
/// una copia de su contenido. Un dia sin turno no aparece en <see cref="CuadroSemanalTurnos.Dias"/>
/// (ausencia = vacio).
/// </summary>
/// <param name="Semana">Numero de semana de la plantilla (1..N).</param>
/// <param name="Dia">Numero ISO 8601 del dia (1 = lunes .. 7 = domingo, DiaSemana.Numero mapeado en
/// Apply -- ReadModels es isla y no referencia el VO).</param>
/// <param name="TurnoId">El Guid del turno como string -- exactamente el Id de FichaTurno, listo
/// para LoadAsync&lt;FichaTurno&gt; en #625.</param>
public sealed record DiaDelCuadro(int Semana, int Dia, string TurnoId);
