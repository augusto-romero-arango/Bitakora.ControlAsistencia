namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Equals/GetHashCode comparan Descansos y Extras por valor y excluyen Descripcion (texto derivado de
/// FranjaOrdinaria.ToString()). Sede (null = sin sede) es la EFECTIVA ya resuelta por la cascada
/// (TurnoProgramado.ConSedePorDefecto), gemela de FranjaProgramada.Sede: este DTO nunca aplica la
/// cascada, y Sede si entra en la igualdad.
/// </summary>
public record DetalleFranjaOrdinaria(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<DetalleSubFranja> Descansos,
    IReadOnlyList<DetalleSubFranja> Extras,
    string Descripcion,
    DetalleSede? Sede = null)
{
    /// <summary>Los eventos persistidos antes de existir el campo lo traen null: se normaliza a cadena vacia.</summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(DetalleFranjaOrdinaria? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return HoraInicio == other.HoraInicio
            && HoraFin == other.HoraFin
            && DiaOffsetFin == other.DiaOffsetFin
            && Descansos.SequenceEqual(other.Descansos)
            && Extras.SequenceEqual(other.Extras)
            && Sede == other.Sede;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HoraInicio);
        hash.Add(HoraFin);
        hash.Add(DiaOffsetFin);
        foreach (var d in Descansos) hash.Add(d);
        foreach (var e in Extras) hash.Add(e);
        hash.Add(Sede);
        return hash.ToHashCode();
    }
}
