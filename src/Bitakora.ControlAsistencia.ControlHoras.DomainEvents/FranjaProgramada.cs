namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Franja ordinaria propia de este ensamblado. Duplica con paridad exacta de campos a
/// DetalleFranjaOrdinaria (PrivateEvents.Programacion) en vez de importarlo: los tres ensamblados
/// de eventos son islas sin referencias entre si (CA-ADR-0029).
/// </summary>
/// <remarks>
/// Equals/GetHashCode comparan Descansos/Extras POR VALOR (el record los compararia por referencia)
/// y EXCLUYEN Descripcion (dato derivado); Sede si entra, es identidad de la franja. Mismo criterio
/// que DetalleFranjaOrdinaria y FranjaProgramada de Programacion.DomainEvents.
/// Sede null = sin sede asignada.
/// </remarks>
public record FranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaProgramada> Descansos,
    IReadOnlyList<SubFranjaProgramada> Extras,
    string Descripcion,
    SedeProgramada? Sede = null)
{
    /// <summary>
    /// Los eventos anteriores al campo deserializan con null, que la propiedad no anulable
    /// desmentiria: se normaliza a cadena vacia.
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public FranjaProgramada Desplazar(int minutos)
    {
        var (horaInicio, _) = Tramo.DesdeMinutosAbsolutos(Tramo.MinutosAbsolutos(HoraInicio, 0) + minutos);
        var (horaFin, offsetFin) = Tramo.DesdeMinutosAbsolutos(
            Tramo.MinutosAbsolutos(HoraFin, DiaOffsetFin) + minutos);
        return this with
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetFin = offsetFin,
            Descansos = Descansos.Select(d => d.Desplazar(minutos)).ToList(),
            Extras = Extras.Select(e => e.Desplazar(minutos)).ToList()
        };
    }

    public virtual bool Equals(FranjaProgramada? other)
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

    /// <summary>
    /// Tramos en orden cronologico: uno tipado por cada descanso o extra (que pueden intercalarse
    /// entre si) y uno Ordinaria en cada hueco entre ellos.
    /// </summary>
    /// <remarks>
    /// La hora de inicio pertenece siempre al dia de asignacion (offset 0); solo la de fin lleva
    /// <see cref="DiaOffsetFin"/>.
    /// La sede se estampa en un unico paso al final, no en cada <c>new Tramo(...)</c>: un tramo
    /// nuevo en el recorte la hereda por construccion.
    /// </remarks>
    internal IEnumerable<Tramo> Segmentar() =>
        Recortar().Select(tramo => tramo with { Sede = Sede });

    private IEnumerable<Tramo> Recortar()
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
