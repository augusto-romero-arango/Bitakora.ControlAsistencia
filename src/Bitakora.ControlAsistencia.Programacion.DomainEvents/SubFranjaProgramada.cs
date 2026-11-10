namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Paridad exacta de campos con DetalleSubFranja (PrivateEvents). "Programada" es calificador de
/// dominio, distinto de "Detalle*" (el DTO de bus). Equals/GetHashCode excluyen Descripcion (derivado).
/// </summary>
public record SubFranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin,
    string Descripcion)
{
    /// <summary>Los eventos persistidos antes de existir el campo lo traen null: se normaliza a cadena vacia.</summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(SubFranjaProgramada? other) =>
        other is not null
        && HoraInicio == other.HoraInicio
        && HoraFin == other.HoraFin
        && DiaOffsetInicio == other.DiaOffsetInicio
        && DiaOffsetFin == other.DiaOffsetFin;

    public override int GetHashCode() =>
        HashCode.Combine(HoraInicio, HoraFin, DiaOffsetInicio, DiaOffsetFin);
}
