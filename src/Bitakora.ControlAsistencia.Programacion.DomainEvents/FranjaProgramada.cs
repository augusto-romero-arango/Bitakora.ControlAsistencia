namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Representacion plana de una franja ordinaria programada, propia del dominio Programacion.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): payload propio de este ensamblado con
/// paridad exacta de campos con DetalleFranjaOrdinaria (PrivateEvents), incluyendo que sus hijas
/// (Descansos/Extras) tipan con SubFranjaProgramada, no con DetalleSubFranja.
/// FranjaOrdinaria.ToDetalle() retorna este tipo -- sin abrir sus campos privados
/// (MEF-ADR-0012, Tell-don't-Ask), la conversion sigue viviendo en el VO rico.
///
/// Equals/GetHashCode propios comparan Descansos/Extras POR VALOR (SequenceEqual, el record por
/// defecto compararia por referencia -- ADR-0015) y EXCLUYEN Descripcion (dato derivado, no
/// identidad de la franja) -- mismo criterio que DetalleFranjaOrdinaria (issues #129 y #288).
///
/// Issue #335: Sede es campo aditivo y opcional (null = sin sede asignada). A diferencia de
/// Descripcion, SI entra en Equals/GetHashCode -- es dato de identidad de la franja, no un
/// derivado.
///
/// Issue #341: el campo tiene ya su gemelo de payload por rol (CA-ADR-0029 decision #5) en
/// DetalleFranjaOrdinaria.Sede (PrivateEvents, tipado DetalleSede) -- la divergencia temporal que
/// #335 abrio quedo cerrada y FranjaProgramadaParidadConDetalleFranjaOrdinariaTests vuelve a
/// exigir paridad exacta. Su significado depende del evento que lo lleva: en el snapshot del
/// catalogo es la sede PREARMADA por el diseno del turno; dentro de ProgramacionTurnoSolicitada es
/// la sede EFECTIVA que resolvio la cascada (TurnoProgramado.ConSedePorDefecto).
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
