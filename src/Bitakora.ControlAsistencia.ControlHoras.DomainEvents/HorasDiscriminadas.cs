namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.HorasDiscriminadas
// (payload por rol, MEF-ADR-0039 decision #6).
//
// El record por defecto compara las colecciones por referencia; estos overrides las comparan por valor.
public record HorasDiscriminadas(
    IReadOnlyDictionary<string, decimal> HorasPorConcepto,
    IReadOnlyList<string> Trazabilidad)
{
    public virtual bool Equals(HorasDiscriminadas? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return HorasPorConceptoIguales(other.HorasPorConcepto)
            && Trazabilidad.SequenceEqual(other.Trazabilidad);
    }

    private bool HorasPorConceptoIguales(IReadOnlyDictionary<string, decimal> otros) =>
        HorasPorConcepto.Count == otros.Count
        && HorasPorConcepto.All(par => otros.TryGetValue(par.Key, out var valor) && valor == par.Value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        var hashHoras = 0;
        foreach (var par in HorasPorConcepto)
            hashHoras ^= HashCode.Combine(par.Key, par.Value);
        hash.Add(hashHoras);
        foreach (var nota in Trazabilidad) hash.Add(nota);
        return hash.ToHashCode();
    }
}
