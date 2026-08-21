namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano del dia que viaja en DiaDepurado. Debe permanecer 100% primitivo: el resolver custom
// de Marten NO se aplica al canal de publicacion a Service Bus, asi que un campo rico llegaria lossy
// al consumidor (MEF-ADR-0012, frontera event store vs bus).
//
// HorasPorConcepto: clave = Concepto.ToString() ("OrdinariaDiurna", ...) o la clave literal "Retardo";
// valor = horas liquidables del dia, producidas via HorasLiquidables (unico punto de conversion del
// BC). Todo el diccionario habla el mismo idioma, incluida la clave "Retardo".
// Trazabilidad narra en minutos e intervalos a proposito: es memoria de auditoria del calculo, no
// dato operable del mundo humano.
//
// El record por defecto compara HorasPorConcepto/Trazabilidad por referencia; los overrides de
// Equals/GetHashCode las comparan por valor sin perder la forma de record (MEF-ADR-0012, nota sobre
// equality).
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

    // Igualdad de diccionario independiente del orden de insercion: mismo tamanio y cada par presente
    // con el mismo valor en el otro.
    private bool HorasPorConceptoIguales(IReadOnlyDictionary<string, decimal> otros) =>
        HorasPorConcepto.Count == otros.Count
        && HorasPorConcepto.All(par => otros.TryGetValue(par.Key, out var valor) && valor == par.Value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        // El diccionario se hashea con XOR de los pares para que el hash no dependa del orden de
        // enumeracion (objetos iguales deben producir el mismo hash). Trazabilidad es ordenada.
        var hashHoras = 0;
        foreach (var par in HorasPorConcepto)
            hashHoras ^= HashCode.Combine(par.Key, par.Value);
        hash.Add(hashHoras);
        foreach (var nota in Trazabilidad) hash.Add(nota);
        return hash.ToHashCode();
    }
}
