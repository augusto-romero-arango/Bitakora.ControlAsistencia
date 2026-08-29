namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Superficie de INVESTIGACION del Aprobador: plan contra realidad, cuales marcaciones contaron y
/// por que la maquina calculo (o no) esas horas. Complementa a AsistenciaDiaria -- la superficie de
/// DECISION, materializada --, que deja estos campos fuera a proposito.
///
/// Vista VIVA de la via (b1) (skills/projections/read-apis.md, MEF-ADR-0035): NINGUN worker la
/// materializa -- la produce DiaCalculadoAggregateRoot.GenerarDepuracionDelDia() sobre el aggregate
/// hidratado por session.Events.AggregateStreamAsync (MEF-ADR-0015). De ahi que sea un record plano
/// SIN partial y sin Create/Apply/ShouldDelete: no es N1/N2 de Marten, es el tipo de respuesta HTTP
/// -- excepcion justificada de MEF-ADR-0041 decision 4, porque el aggregate tiene todo su estado
/// privado (MEF-ADR-0012) y no es serializable.
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
    IReadOnlyList<string> Trazabilidad);

/// <summary>
/// Espejo por rol del homonimo de ControlHoras.DomainEvents (MEF-ADR-0039 decision 6). Ningun campo
/// tipa contra DomainEvents: ReadModels es la cuarta isla, cero referencias de proyecto.
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
/// Espejo por rol del homonimo de ControlHoras.DomainEvents, con campos propios de esta isla:
/// Usada, que deriva el generador y ningun cliente recalcula, y la sede marcada CRUDA de esta
/// marcacion -- sin confrontar contra el plan, para que el Aprobador vea de donde salio cada
/// candidata de FranjaDepurada.CandidatasDeSede. TODAS las marcaciones del dia viajan
/// aqui, en orden cronologico -- las descartadas se muestran igual, para que el Aprobador vea que la
/// maquina las dejo afuera.
/// </summary>
public sealed record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    bool Usada,
    string? CodigoSede,
    string? NombreSede,
    string? CentroDeCostos);
