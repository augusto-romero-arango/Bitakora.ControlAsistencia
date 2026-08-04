namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana de una sub-franja (descanso o extra) que viaja en eventos entre dominios.
/// </summary>
/// <remarks>
/// Issue #288: Descripcion es la representacion textual normalizada (formato tecnico del tipo rico
/// SubFranja.ToString()), persistida en vez de calculada al leer -- decision consciente documentada
/// en el issue (Rule of Three, MEF-ADR-0018; el read-side no puede rehidratar el tipo rico).
/// Equals/GetHashCode propios EXCLUYEN Descripcion: es un dato derivado, no identidad de la
/// sub-franja. Mismo patron que el override de DetalleFranjaOrdinaria (issue #129).
/// </remarks>
public record DetalleSubFranja(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin,
    string Descripcion)
{
    public virtual bool Equals(DetalleSubFranja? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return HoraInicio == other.HoraInicio
            && HoraFin == other.HoraFin
            && DiaOffsetInicio == other.DiaOffsetInicio
            && DiaOffsetFin == other.DiaOffsetFin;
    }

    public override int GetHashCode() =>
        HashCode.Combine(HoraInicio, HoraFin, DiaOffsetInicio, DiaOffsetFin);
}
