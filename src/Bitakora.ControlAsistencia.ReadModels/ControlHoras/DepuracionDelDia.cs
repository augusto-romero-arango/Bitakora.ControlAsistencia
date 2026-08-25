namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Superficie de INVESTIGACION del Aprobador (issue #429): plan contra realidad, cuales
/// marcaciones contaron y por que la maquina calculo (o no) esas horas -- lo que AsistenciaDiaria
/// (issue #426, materializada) deja fuera a proposito porque el 95% de los dias nadie los abre.
///
/// Vista VIVA de la via (b1) (skills/projections/read-apis.md, MEF-ADR-0035): NINGUN worker de
/// proyecciones la materializa -- la produce DiaCalculadoAggregateRoot.GenerarDepuracionDelDia() al
/// vuelo, sobre el aggregate hidratado por session.Events.AggregateStreamAsync (MEF-ADR-0015, mismo
/// mecanismo Live que ya usa el write-side). Record plano SIN partial, sin Create/Apply/ShouldDelete
/// propios (esto no es N1/N2 de Marten): tipo de respuesta HTTP directo -- excepcion justificada de
/// MEF-ADR-0041 decision 4, porque el aggregate tiene todo su estado privado (Tell-don't-Ask,
/// MEF-ADR-0012) y no es serializable.
///
/// IdentificacionColaborador/NombreColaborador van null cuando el dia nacio solo por marcacion (sin
/// ResumenColaborador en el evento, CA-4). Usada la deriva el generador una sola vez -- igualdad
/// exacta de Timestamp contra la Entrada o Salida de alguna FranjaDepurada -- nunca la UI.
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
/// Espejo por rol de ControlHoras.DomainEvents.FranjaDepurada -- tercer espejo del mismo termino del
/// glosario (PrivateEvents -> DomainEvents -> ReadModels, MEF-ADR-0039 decision 6). Ningun campo tipa
/// contra DomainEvents: ReadModels es la cuarta isla, cero referencias de proyecto.
/// </summary>
public sealed record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala);

/// <summary>
/// Espejo por rol de ControlHoras.DomainEvents.MarcacionDelDia, con un campo propio de esta isla:
/// Usada. TODAS las marcaciones del dia viajan aqui, en orden cronologico (contrato del evento) --
/// las descartadas se muestran igual, para que el Aprobador vea que la maquina las dejo afuera.
/// </summary>
public sealed record MarcacionDelDia(DateTime Timestamp, string? Tipo, bool Usada);
