namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Aprobado todavia no lo produce ningun evento: la proyeccion solo emite Provisional. El valor se
/// declara porque el ciclo del lenguaje ubicuo lo contempla, no porque ya se materialice.
/// </summary>
public enum EstadoAsistencia
{
    Provisional,
    Aprobado
}

public enum PlanDelDia
{
    ConJornada,
    Descanso,
    SinProgramar
}

/// <summary>
/// Superficie de DECISION del Aprobador: fila liviana por colaborador+dia que responde "a quien
/// miro primero" y si el dia esta listo para aprobar. Franjas, marcaciones crudas y nombre del
/// colaborador quedan fuera a proposito -- son superficie de investigacion, se leen en vivo del
/// aggregate o las compone la UI.
///
/// Record plano SIN partial: el comportamiento de proyeccion vive en AsistenciaDiariaProjection, en
/// el worker. Ningun campo puede tipar contra ControlHoras.DomainEvents -- ReadModels es la cuarta
/// isla, cero referencias de proyecto (MEF-ADR-0041 decision 2).
///
/// Id es el stream key que computa el write-side (DiaCalculadoAggregateRoot.ComputarStreamId): la
/// vista lo consume tal cual, nunca lo re-computa (MEF-ADR-0037).
///
/// Las cuatro banderas se derivan una sola vez en Create/Apply; ninguna consulta las recalcula en
/// query-time -- la fila no carga los datos que harian falta para hacerlo. HorasPorConcepto llega ya
/// filtrada desde el productor (DesgloseHoras.Discriminar): no re-filtrar aqui.
/// </summary>
public sealed record AsistenciaDiaria(
    string Id,
    string CodigoColaborador,
    DateOnly Fecha,
    EstadoAsistencia Estado,
    PlanDelDia Plan,
    string? NombreTurno,
    bool NoSePresento,
    bool FranjasIncompletas,
    bool VinoEnDescanso,
    bool TrabajoSinProgramacion,
    IReadOnlyDictionary<string, decimal> HorasPorConcepto);
