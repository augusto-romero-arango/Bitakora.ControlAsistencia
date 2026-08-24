using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Fila de un dia en la respuesta de ListarAsistenciasDiarias (issue #427). DTO de respuesta
/// propio -- excepcion bajo Rule of Three (MEF-ADR-0041 decision 4): la fila sintetica no existe
/// como read model y el estado presentado de tres valores (<see cref="EstadoAsistenciaPresentado"/>)
/// no tiene forma en AsistenciaDiaria (#426) -- devolver el documento crudo no puede expresar la
/// respuesta.
///
/// Plan/NombreTurno/las cuatro anomalias/HorasPorConcepto reusan los tipos ya definidos en
/// ReadModels.ControlHoras (mismo shape que AsistenciaDiaria) cuando el dia tiene documento; una
/// fila sintetica (issue #427, CA-2) los sintetiza: Plan = SinProgramar, NombreTurno = null, las
/// cuatro anomalias en false, HorasPorConcepto vacio.
/// </summary>
public sealed record FilaAsistenciaDiaria(
    DateOnly Fecha,
    EstadoAsistenciaPresentado Estado,
    PlanDelDia Plan,
    string? NombreTurno,
    bool NoSePresento,
    bool FranjasIncompletas,
    bool VinoEnDescanso,
    bool TrabajoSinProgramacion,
    IReadOnlyDictionary<string, decimal> HorasPorConcepto);
