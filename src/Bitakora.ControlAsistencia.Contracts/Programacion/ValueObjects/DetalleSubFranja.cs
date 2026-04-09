namespace Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

/// <summary>
/// Representacion plana de una sub-franja (descanso o extra) que viaja en eventos entre dominios.
/// </summary>
public record DetalleSubFranja(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);
