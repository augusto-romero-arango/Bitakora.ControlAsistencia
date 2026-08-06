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

    /// <summary>
    /// Recorta la franja por sus descansos y extras y devuelve los tramos resultantes en orden
    /// cronologico (issue #327): un tramo tipado por cada sub-franja y un tramo Ordinaria en cada
    /// hueco entre ellas. El glosario define descansos y extras como contenidos en la ordinaria, y
    /// el algoritmo los trata igual -- por eso se fusionan en una sola secuencia ordenada, que
    /// admite ademas que se intercalen entre si.
    /// </summary>
    /// <remarks>
    /// La hora de inicio de la franja pertenece siempre al dia de asignacion (offset 0); solo su
    /// hora de fin lleva <see cref="DiaOffsetFin"/>. Cada sub-franja resuelve sus dos offsets
    /// propios (<see cref="SubFranjaProgramada.ATramo"/>).
    /// </remarks>
    internal IEnumerable<Tramo> Segmentar()
    {
        var subFranjas = Descansos
            .Select(descanso => descanso.ATramo(TipoBloque.Descanso))
            .Concat(Extras.Select(extra => extra.ATramo(TipoBloque.Extra)))
            .OrderBy(sub => sub.Inicio);

        var finFranja = Tramo.MinutosAbsolutos(HoraFin, DiaOffsetFin);
        var cursor = Tramo.MinutosAbsolutos(HoraInicio, 0);
        foreach (var sub in subFranjas)
        {
            if (sub.Inicio > cursor)
                yield return new Tramo(TipoBloque.Ordinaria, cursor, sub.Inicio);
            yield return sub;
            cursor = sub.Fin;
        }

        if (cursor < finFranja)
            yield return new Tramo(TipoBloque.Ordinaria, cursor, finFranja);
    }
}
