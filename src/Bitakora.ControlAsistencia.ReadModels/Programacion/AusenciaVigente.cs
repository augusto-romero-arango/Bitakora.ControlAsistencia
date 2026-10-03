namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Una ausencia con al menos un dia vigente, tal como la consulta el Programador al preguntar
/// "quien falta en este periodo". Id es el AusenciaId: el stream es por colaborador y el documento
/// por ausencia (N2). PrimerDiaVigente/UltimoDiaVigente limitan TramosVigentes para filtrar por
/// cruce con el periodo.
/// </summary>
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
