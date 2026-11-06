namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public enum TipoBloqueProgramado
{
    Ordinaria,
    Descanso,
    Extra
}

/// <summary>
/// Tramo absoluto de un turno programado en una fecha (<see cref="TurnoProgramado.Segmentar"/>).
/// Lectura pura: no se persiste.
/// </summary>
public sealed record BloqueProgramado(TipoBloqueProgramado Tipo, DateTime Inicio, DateTime Fin, SedeProgramada? Sede);
