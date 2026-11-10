namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Superficie de INVESTIGACION del Aprobador: plan contra realidad, cuales marcaciones contaron y
/// por que la maquina calculo (o no) esas horas. Complementa a AsistenciaDiaria, la superficie de
/// DECISION.
///
/// Vista viva: ningun worker la materializa. La produce DiaCalculadoAggregateRoot.GenerarDepuracionDelDia()
/// sobre el aggregate hidratado, y es el tipo de respuesta HTTP porque el aggregate tiene su estado
/// privado y no es serializable (MEF-ADR-0041 decision 4).
///
/// IdentificacionColaborador/NombreColaborador van null cuando el dia nacio solo por marcacion, sin
/// ResumenColaborador en el evento.
/// </summary>
public sealed record DepuracionDelDia(
    string CodigoColaborador,
    DateOnly Fecha,
    string? IdentificacionColaborador,
    string? NombreColaborador,
    EstadoAsistencia Estado,
    PlanDelDia Plan,
    string? NombreTurno,
    IReadOnlyList<FranjaDepurada> Franjas,
    IReadOnlyList<MarcacionDelDia> Marcaciones,
    IReadOnlyDictionary<string, decimal> HorasPorConcepto,
    IReadOnlyList<string> Trazabilidad,
    string? MotivoAusencia = null);

/// <summary>
/// Espejo por rol del homonimo de ControlHoras.DomainEvents (MEF-ADR-0039 decision 6).
///
/// SedeEfectiva y CandidatasDeSede son excluyentes: sin conflicto va la efectiva y la lista vacia;
/// con conflicto va null y TODAS las candidatas, porque la maquina no elige sede por el Aprobador.
/// </summary>
public sealed record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala,
    SedeDeFranja? SedeEfectiva,
    bool EnConflictoDeSede,
    IReadOnlyList<SedeDeFranja> CandidatasDeSede);

/// <summary>
/// Candidata de sede de una franja: la programada del plan o la marcada en una de sus marcaciones.
/// Nombre y CentroDeCostos viajan tal como se estamparon en su fuente -- nunca un lookup al maestro
/// de sedes (la verdad viaja en el evento), de ahi que puedan faltar mientras Codigo no.
/// </summary>
public sealed record SedeDeFranja(string Codigo, string? Nombre, string? CentroDeCostos);

/// <summary>
/// Espejo por rol del homonimo de ControlHoras.DomainEvents, mas Usada (la deriva el generador) y la
/// sede marcada CRUDA, sin confrontar contra el plan, para ver de donde salio cada candidata de
/// FranjaDepurada.CandidatasDeSede. Van TODAS las marcaciones del dia en orden cronologico, tambien
/// las descartadas; Pertenencia dice por que se descarto. En un dia sin jornada valida solo se marca
/// DeTurnoVecino; las demas van en null.
/// </summary>
public sealed record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    bool Usada,
    string? CodigoSede,
    string? NombreSede,
    string? CentroDeCostos,
    PertenenciaMarcacion? Pertenencia = null);

/// <summary>
/// Paridad de nombres con el PertenenciaMarcacion del dominio: la vista lo lee por nombre desde el
/// string del evento.
/// </summary>
public enum PertenenciaMarcacion
{
    DelTurno,
    FueraDeCobertura,
    Ambigua,
    DeTurnoVecino
}
