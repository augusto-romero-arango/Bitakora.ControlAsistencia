namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #425: payload propio de esta isla, espejo de PrivateEvents.ControlHoras.HorasDiscriminadas
// (payload por rol, MEF-ADR-0039 decision #6).
//
// El record por defecto compara HorasPorConcepto/Trazabilidad por referencia; estos overrides las
// comparan por valor (MEF-ADR-0012, nota sobre equality: un record con colecciones promete una
// igualdad que el compilador no genera).
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
