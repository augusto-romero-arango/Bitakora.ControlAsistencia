namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana de una sub-franja (descanso o extra) propia de este ensamblado
/// (issue #322, payload por rol -- CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). Duplica
/// con paridad exacta de campos a DetalleSubFranja (PrivateEvents.Programacion) en vez de
/// importarlo: los tres ensamblados de eventos son tres islas sin referencias entre si.
/// </summary>
/// <remarks>
/// Equals/GetHashCode propios EXCLUYEN Descripcion (dato derivado, no identidad de la
/// sub-franja) -- mismo criterio que DetalleSubFranja (issue #288) y SubFranjaProgramada
/// (Programacion.DomainEvents, issue #319).
/// </remarks>
public record SubFranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin,
    string Descripcion)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia -- mismo criterio que DetalleSubFranja (issue #288).
    /// </summary>
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
