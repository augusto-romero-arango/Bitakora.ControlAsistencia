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
    SinProgramar,
    Ausencia
}

/// <summary>
/// Superficie de DECISION del Aprobador: fila liviana por colaborador+dia que responde "a quien
/// miro primero" y si el dia esta listo para aprobar. Franjas, marcaciones crudas y nombre del
/// colaborador quedan fuera a proposito: son superficie de investigacion (DepuracionDelDia).
///
/// Id es el stream key de DiaCalculadoAggregateRoot.ComputarStreamId, tal cual; nunca se re-computa
/// (MEF-ADR-0037).
///
/// Las banderas se derivan una sola vez al proyectar: la fila no carga los datos para recalcularlas
/// en la consulta. HorasPorConcepto llega ya filtrada desde el productor: no re-filtrar aqui.
///
/// ConflictoDeSedePendiente: mientras este en true el dia no es aprobable (glosario "Conflicto de
/// sede"). Es del DIA completo, a diferencia del homonimo EnConflictoDeSede de
/// DepuracionDelDia.FranjaDepurada, que es por franja.
///
/// MarcacionAmbiguaPendiente: el dia tiene al menos una marcacion que el Aprobador debe asignar a
/// uno de los dos turnos que la disputan. Un dia aprobado la lleva en false.
/// </summary>
[MaterializadoPorProyeccion]
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
    bool ConflictoDeSedePendiente,
    IReadOnlyDictionary<string, decimal> HorasPorConcepto,
    string? MotivoAusencia = null,
    bool VinoEnAusencia = false,
    bool MarcacionAmbiguaPendiente = false);
