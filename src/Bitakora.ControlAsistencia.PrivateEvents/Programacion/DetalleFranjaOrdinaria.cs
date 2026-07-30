namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana de una franja ordinaria que viaja en eventos entre dominios.
/// </summary>
/// <remarks>
/// Issue #129: override de Equals/GetHashCode para comparar Descansos y Extras con SequenceEqual.
/// El record por defecto compara colecciones por referencia (ADR-0015 advierte sobre records con
/// IReadOnlyList que prometen igualdad por valor que no cumplen). Esta intervencion preserva la
/// forma de record (constructor primario publico, properties get-only) y corrige el bug.
/// </remarks>
public record DetalleFranjaOrdinaria(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<DetalleSubFranja> Descansos,
    IReadOnlyList<DetalleSubFranja> Extras)
{
    public virtual bool Equals(DetalleFranjaOrdinaria? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return HoraInicio == other.HoraInicio
            && HoraFin == other.HoraFin
            && DiaOffsetFin == other.DiaOffsetFin
            && Descansos.SequenceEqual(other.Descansos)
            && Extras.SequenceEqual(other.Extras);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HoraInicio);
        hash.Add(HoraFin);
        hash.Add(DiaOffsetFin);
        foreach (var d in Descansos) hash.Add(d);
        foreach (var e in Extras) hash.Add(e);
        return hash.ToHashCode();
    }
}
