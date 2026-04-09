namespace Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

/// <summary>
/// Representacion plana de una franja ordinaria que viaja en eventos entre dominios.
/// </summary>
public record DetalleFranjaOrdinaria(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<DetalleSubFranja> Descansos,
    IReadOnlyList<DetalleSubFranja> Extras);
