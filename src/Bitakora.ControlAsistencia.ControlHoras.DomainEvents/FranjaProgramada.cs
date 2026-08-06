namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana de una franja ordinaria propia de este ensamblado (issue #322, payload
/// por rol -- CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). Duplica con paridad exacta de
/// campos a DetalleFranjaOrdinaria (PrivateEvents.Programacion) en vez de importarlo: los tres
/// ensamblados de eventos son tres islas sin referencias entre si.
/// </summary>
/// <remarks>
/// Equals/GetHashCode propios comparan Descansos/Extras POR VALOR (SequenceEqual, el record por
/// defecto compararia por referencia -- ADR-0015) y EXCLUYEN Descripcion (dato derivado, no
/// identidad de la franja) -- mismo criterio que DetalleFranjaOrdinaria (issues #129 y #288) y
/// que FranjaProgramada (Programacion.DomainEvents, issue #319).
/// </remarks>
public record FranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaProgramada> Descansos,
    IReadOnlyList<SubFranjaProgramada> Extras,
    string Descripcion)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia -- mismo criterio que DetalleFranjaOrdinaria (issue #288).
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(FranjaProgramada? other)
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
