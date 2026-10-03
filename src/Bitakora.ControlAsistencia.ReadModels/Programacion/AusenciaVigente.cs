namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Una ausencia con al menos un dia vigente, tal como la consulta el Programador al preguntar
/// "quien falta en este periodo" (MEF-ADR-0041: nombre del lenguaje ubicuo, sin sufijo de
/// implementacion).
/// </summary>
/// <remarks>
/// Record plano SIN partial ni comportamiento (MEF-ADR-0035): el mapeo evento -> vista vive en
/// AusenciaVigenteProjection (worker). Id es el AusenciaId (N2: el stream es por colaborador, el
/// documento por ausencia). Isla ReadModels: cero ProjectReference a DomainEvents.
/// PrimerDiaVigente/UltimoDiaVigente son los limites de TramosVigentes, para filtrar por cruce con
/// el periodo consultado.
/// </remarks>
public sealed record AusenciaVigente(
    Guid Id,
    string CodigoColaborador,
    string NombreCompleto,
    string Motivo,
    IReadOnlyList<TramoDeAusencia> TramosVigentes,
    DateOnly PrimerDiaVigente,
    DateOnly UltimoDiaVigente);

/// <summary>Tramo contiguo de dias vigentes de una ausencia, ambos extremos inclusive.</summary>
public sealed record TramoDeAusencia(DateOnly Desde, DateOnly Hasta);
