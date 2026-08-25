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
/// </summary>
public sealed record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala);

/// <summary>
/// Espejo por rol del homonimo de ControlHoras.DomainEvents, con un campo propio de esta isla:
/// Usada, que deriva el generador y ningun cliente recalcula. TODAS las marcaciones del dia viajan
/// aqui, en orden cronologico -- las descartadas se muestran igual, para que el Aprobador vea que la
/// maquina las dejo afuera.
/// </summary>
public sealed record MarcacionDelDia(DateTime Timestamp, string? Tipo, bool Usada);
