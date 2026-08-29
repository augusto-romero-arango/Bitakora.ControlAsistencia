using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// DTO de respuesta propio en vez de servir AsistenciaDiaria crudo -- excepcion justificada de
/// MEF-ADR-0041 decision 4: la fila sintetica no existe como read model y el estado presentado
/// tiene un valor (SinDatos) que el read model no tiene.
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
    bool ConflictoDeSedePendiente,
    IReadOnlyDictionary<string, decimal> HorasPorConcepto);
