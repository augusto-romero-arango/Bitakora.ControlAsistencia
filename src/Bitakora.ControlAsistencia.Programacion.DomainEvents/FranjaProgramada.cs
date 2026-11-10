namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Paridad exacta de campos con DetalleFranjaOrdinaria (PrivateEvents), con hijas SubFranjaProgramada;
/// FranjaProgramadaParidadConDetalleFranjaOrdinariaTests la exige. Equals/GetHashCode comparan
/// Descansos/Extras por valor, incluyen Sede y excluyen Descripcion (dato derivado).
/// </summary>
/// <remarks>
/// Sede (null = sin sede) depende del evento que la lleva: en el snapshot del catalogo es la PREARMADA
/// por el diseno del turno; en ProgramacionTurnoSolicitada es la EFECTIVA que resolvio la cascada
/// (TurnoProgramado.ConSedePorDefecto).
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
    /// <summary>Los eventos persistidos antes de existir el campo lo traen null: se normaliza a cadena vacia.</summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

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

    private const int MinutosPorDia = 1440;

    /// <summary>
    /// Recorta la franja por sus descansos y extras en bloques absolutos contra
    /// <paramref name="fecha"/>, el dia en que empieza. Todos los bloques llevan la sede de la
    /// franja. No rompe en la medianoche: el bloque nocturno termina al dia siguiente.
    /// </summary>
    internal IEnumerable<BloqueProgramado> Segmentar(DateOnly fecha)
    {
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        BloqueProgramado Bloque(TipoBloqueProgramado tipo, int inicio, int fin) =>
            new(tipo, medianoche.AddMinutes(inicio), medianoche.AddMinutes(fin), Sede);

        var subFranjas = Descansos.Select(d => Tramo(TipoBloqueProgramado.Descanso, d))
            .Concat(Extras.Select(x => Tramo(TipoBloqueProgramado.Extra, x)))
            .OrderBy(sub => sub.Inicio);

        var cursor = Minutos(HoraInicio, 0);
        foreach (var sub in subFranjas)
        {
            if (sub.Inicio > cursor)
                yield return Bloque(TipoBloqueProgramado.Ordinaria, cursor, sub.Inicio);
            yield return Bloque(sub.Tipo, sub.Inicio, sub.Fin);
            cursor = sub.Fin;
        }

        var finFranja = Minutos(HoraFin, DiaOffsetFin);
        if (cursor < finFranja)
            yield return Bloque(TipoBloqueProgramado.Ordinaria, cursor, finFranja);
    }

    private static (TipoBloqueProgramado Tipo, int Inicio, int Fin) Tramo(
        TipoBloqueProgramado tipo, SubFranjaProgramada sub) =>
        (tipo, Minutos(sub.HoraInicio, sub.DiaOffsetInicio), Minutos(sub.HoraFin, sub.DiaOffsetFin));

    private static int Minutos(TimeOnly hora, int diaOffset) =>
        hora.Hour * 60 + hora.Minute + diaOffset * MinutosPorDia;
}
