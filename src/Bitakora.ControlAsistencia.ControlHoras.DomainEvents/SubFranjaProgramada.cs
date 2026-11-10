namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Sub-franja (descanso o extra) propia de este ensamblado. Duplica con paridad exacta de campos
/// a DetalleSubFranja (PrivateEvents.Programacion) en vez de importarlo: los tres ensamblados de
/// eventos son islas sin referencias entre si (CA-ADR-0029 decision #5).
/// </summary>
/// <remarks>
/// Equals/GetHashCode excluyen Descripcion (dato derivado, no identidad de la sub-franja), igual
/// que DetalleSubFranja y SubFranjaProgramada de Programacion.DomainEvents.
/// </remarks>
public record SubFranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin,
    string Descripcion)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo y STJ deja null: se normaliza a
    /// cadena vacia para no mentir en un string no anulable.
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public SubFranjaProgramada Desplazar(int minutos)
    {
        var (horaInicio, offsetInicio) = Tramo.DesdeMinutosAbsolutos(
            Tramo.MinutosAbsolutos(HoraInicio, DiaOffsetInicio) + minutos);
        var (horaFin, offsetFin) = Tramo.DesdeMinutosAbsolutos(
            Tramo.MinutosAbsolutos(HoraFin, DiaOffsetFin) + minutos);
        return this with
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetInicio = offsetInicio,
            DiaOffsetFin = offsetFin
        };
    }

    public virtual bool Equals(SubFranjaProgramada? other) =>
        other is not null
        && HoraInicio == other.HoraInicio
        && HoraFin == other.HoraFin
        && DiaOffsetInicio == other.DiaOffsetInicio
        && DiaOffsetFin == other.DiaOffsetFin;

    public override int GetHashCode() =>
        HashCode.Combine(HoraInicio, HoraFin, DiaOffsetInicio, DiaOffsetFin);

    /// <summary>
    /// Cada extremo resuelve su propio offset de dia contra la fecha de asignacion, no contra el
    /// inicio de la franja que la contiene.
    /// </summary>
    internal Tramo ATramo(TipoBloque tipo) => new(
        tipo,
        Tramo.MinutosAbsolutos(HoraInicio, DiaOffsetInicio),
        Tramo.MinutosAbsolutos(HoraFin, DiaOffsetFin));
}
