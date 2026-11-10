namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Debe permanecer 100% primitivo: el resolver custom de Marten no se aplica al canal de Service Bus y
// un campo rico llegaria lossy al consumidor (MEF-ADR-0012).
//
// HorasPorConcepto: clave = Concepto.ToString() ("OrdinariaDiurna", ...) o la literal "Retardo";
// valor = horas liquidables del dia, producidas via HorasLiquidables (unico punto de conversion del BC).
// Trazabilidad narra en minutos e intervalos a proposito: es memoria de auditoria, no dato operable.
// Equals/GetHashCode comparan ambas colecciones por valor.
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

    // Independiente del orden de insercion.
    private bool HorasPorConceptoIguales(IReadOnlyDictionary<string, decimal> otros) =>
        HorasPorConcepto.Count == otros.Count
        && HorasPorConcepto.All(par => otros.TryGetValue(par.Key, out var valor) && valor == par.Value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        // XOR de los pares: el hash no depende del orden de enumeracion del diccionario.
        var hashHoras = 0;
        foreach (var par in HorasPorConcepto)
            hashHoras ^= HashCode.Combine(par.Key, par.Value);
        hash.Add(hashHoras);
        foreach (var nota in Trazabilidad) hash.Add(nota);
        return hash.ToHashCode();
    }
}
