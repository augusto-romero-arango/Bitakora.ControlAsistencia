namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Estado del ciclo de aprobacion de la fila. Este issue (#426) solo produce Provisional -- Aprobado
/// llegara con el issue de acciones del Aprobador (el evento que lo produce todavia no existe). Se
/// declaran ambos valores desde ya porque el enum describe el ciclo de vida completo del lenguaje
/// ubicuo (Provisional -> Aprobado), no unicamente lo que este issue materializa.
/// </summary>
public enum EstadoAsistencia
{
    Provisional,
    Aprobado
}

/// <summary>
/// Eje 1 de AsistenciaDiaria ("Columna Plan", #427): clasificacion derivada de la senal estructural
/// de DepuracionDiaRecibida -- NombreTurno null = SinProgramar; NombreTurno + franjas vacias =
/// Descanso; NombreTurno + franjas >= 1 = ConJornada. La proyeccion companion (AsistenciaDiariaProjection)
/// es el unico lugar que deriva este valor; el enum en si no tiene comportamiento.
/// </summary>
public enum PlanDelDia
{
    ConJornada,
    Descanso,
    SinProgramar
}

/// <summary>
/// Read model de la superficie de DECISION del Aprobador (issue #426): fila liviana por
/// colaborador+dia que responde "a quien miro primero" y si el dia esta listo para aprobar. La
/// superficie de investigacion (franjas, marcaciones, nombre del colaborador) se lee en vivo del
/// aggregate o se compone en la UI (#428/#429) -- deliberadamente fuera de esta vista (MEF-ADR-0041).
///
/// Record plano SIN partial (MEF-ADR-0035): el comportamiento de proyeccion vive en la clase
/// companion AsistenciaDiariaProjection, en el worker. Vive en ReadModels, la cuarta isla del repo --
/// cero referencias de proyecto: ningun campo de este record importa un tipo de
/// ControlHoras.DomainEvents (MEF-ADR-0041 decision 1).
///
/// Id es el stream key "dc:{CodigoColaborador}:{yyyyMMdd}" que computa
/// DiaCalculadoAggregateRoot.ComputarStreamId (#425) -- la vista lo consume tal cual, nunca lo
/// re-computa (CA-ADR-0031/MEF-ADR-0037).
///
/// NoSePresento/FranjasIncompletas/VinoEnDescanso/TrabajoSinProgramacion son el eje 2 (anomalias ya
/// juzgadas, espejo del eje 2 de #428): se derivan una sola vez en Create/Apply a partir de Plan y
/// del conteo de marcaciones/franjas anomalas -- nunca se recalculan en query-time (MEF-ADR-0041,
/// campos derivados de la necesidad).
///
/// HorasPorConcepto llega ya sparse desde el productor (PR #435, DesgloseHoras.Discriminar()): la
/// proyeccion la copia tal cual, sin re-filtrar.
///
/// Excluidos deliberadamente: nombre del colaborador (composicion de la UI con FichaColaborador,
/// #428), franjas y marcaciones crudas (superficie de investigacion, #429), UltimaDepuracion y
/// NumeroMarcaciones (retirados 2026-08-24, sin decision del Aprobador que los justifique).
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
