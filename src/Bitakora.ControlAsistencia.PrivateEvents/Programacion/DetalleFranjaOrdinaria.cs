namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana de una franja ordinaria que viaja en eventos entre dominios.
/// </summary>
/// <remarks>
/// Issue #129: override de Equals/GetHashCode para comparar Descansos y Extras con SequenceEqual.
/// El record por defecto compara colecciones por referencia (ADR-0015 advierte sobre records con
/// IReadOnlyList que prometen igualdad por valor que no cumplen). Esta intervencion preserva la
/// forma de record (constructor primario publico, properties get-only) y corrige el bug.
///
/// Issue #288: Descripcion (representacion textual normalizada, formato del tipo rico
/// FranjaOrdinaria.ToString()) se agrega como dato derivado persistido. NO se agrega a
/// Equals/GetHashCode: es texto derivado de los demas campos, no identidad de la franja.
///
/// Issue #341: Sede es campo aditivo y opcional (null = sin sede asignada), gemelo deliberado de
/// FranjaProgramada.Sede (Programacion.DomainEvents, issue #335) -- cierra la divergencia que
/// FranjaProgramadaParidadConDetalleFranjaOrdinariaTests toleraba temporalmente. Lleva la sede
/// EFECTIVA ya resuelta por la cascada (SolicitarProgramacionTurnoCommandHandler,
/// TurnoProgramado.ConSedePorDefecto) -- este DTO de bus nunca aplica la cascada, solo transporta
/// el resultado (snapshot: la verdad viaja en el evento). A diferencia de Descripcion, SI entra en
/// Equals/GetHashCode -- es dato de identidad de la franja, no un derivado (mismo criterio que
/// FranjaProgramada.Sede).
/// </remarks>
public record DetalleFranjaOrdinaria(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<DetalleSubFranja> Descansos,
    IReadOnlyList<DetalleSubFranja> Extras,
    string Descripcion,
    DetalleSede? Sede = null)
{
    /// <summary>
    /// Los eventos anteriores al issue #288 no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia, que es la consecuencia que el issue asumio para los eventos ya persistidos.
    /// </summary>
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
