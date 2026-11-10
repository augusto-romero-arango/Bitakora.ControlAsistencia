namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Descripcion (SubFranja.ToString()) se persiste porque el read-side no puede rehidratar el tipo rico;
/// Equals/GetHashCode la excluyen por ser dato derivado.
/// </summary>
public record DetalleSubFranja(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin,
    string Descripcion)
{
    /// <summary>Los eventos persistidos antes de existir el campo lo traen null: se normaliza a cadena vacia.</summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(DetalleSubFranja? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return HoraInicio == other.HoraInicio
            && HoraFin == other.HoraFin
            && DiaOffsetInicio == other.DiaOffsetInicio
            && DiaOffsetFin == other.DiaOffsetFin;
    }

    public override int GetHashCode() =>
        HashCode.Combine(HoraInicio, HoraFin, DiaOffsetInicio, DiaOffsetFin);
}
