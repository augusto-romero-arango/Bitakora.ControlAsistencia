namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Representacion plana de una sub-franja (descanso o extra) programada, propia del dominio
/// Programacion.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): payload propio de este ensamblado con
/// paridad exacta de campos con DetalleSubFranja (PrivateEvents). SubFranja.ToDetalle() retorna
/// este tipo -- sin abrir sus campos privados (MEF-ADR-0012, Tell-don't-Ask), la conversion sigue
/// viviendo en el VO rico.
///
/// El calificador "Programada" es de dominio ("la franja programada", ver ControlFranja en el
/// glosario), no de infraestructura -- distinto de "Detalle*" (el DTO de bus). Equals/GetHashCode
/// propios EXCLUYEN Descripcion (dato derivado, no identidad de la sub-franja) -- mismo criterio
/// que DetalleSubFranja (issue #288).
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
