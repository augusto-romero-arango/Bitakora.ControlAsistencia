namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Issue #183: payload plano (100% primitivo) del dia, lo que el consumidor del bus interno consume
// realmente. Reemplazo el modelo rico que viajaba en DiaCalculado (DesgloseHoras + DetalleControlFranja),
// el cual dependia del resolver custom de Marten y se serializaba lossy en el canal de
// publicacion a Service Bus (field notes 2026-06-23). Con solo primitivos, ningun consumidor
// depende de nuestra serializacion interna y ese bug se vuelve estructuralmente imposible.
//
// Issue #421: mudado de PublicEvents a PrivateEvents junto con DiaCalculado -> DiaDepurado (evento
// intra-BC, sin cambios de forma). CA-ADR-0029: cada isla de eventos posee sus propios tipos.
// Serializacion: record con primitivos; STJ lo serializa/deserializa nativo SIN ConfigurarSerializacion.
//
// MinutosPorConcepto: clave = Concepto.ToString() ("OrdinariaDiurna", ...) o la clave literal
//   "Retardo"; valor = minutos agregados del dia para esa clave.
// Trazabilidad: textos de auditoria del calculo.
//
// Igualdad por valor de las colecciones via override manual de Equals/GetHashCode (precedente
// DetalleFranjaOrdinaria, #129): el record por defecto compara MinutosPorConcepto/Trazabilidad por
// referencia. El override preserva la forma de record (constructor primario publico) y corrige el bug.
public record HorasDiscriminadas(
    IReadOnlyDictionary<string, int> MinutosPorConcepto,
    IReadOnlyList<string> Trazabilidad)
{
    public virtual bool Equals(HorasDiscriminadas? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return MinutosPorConceptoIguales(other.MinutosPorConcepto)
            && Trazabilidad.SequenceEqual(other.Trazabilidad);
    }

    // Igualdad de diccionario independiente del orden de insercion: mismo tamanio y cada par presente
    // con el mismo valor en el otro.
    private bool MinutosPorConceptoIguales(IReadOnlyDictionary<string, int> otros) =>
        MinutosPorConcepto.Count == otros.Count
        && MinutosPorConcepto.All(par => otros.TryGetValue(par.Key, out var valor) && valor == par.Value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        // El diccionario se hashea con XOR de los pares para que el hash no dependa del orden de
        // enumeracion (objetos iguales deben producir el mismo hash). Trazabilidad es ordenada.
        var hashMinutos = 0;
        foreach (var par in MinutosPorConcepto)
            hashMinutos ^= HashCode.Combine(par.Key, par.Value);
        hash.Add(hashMinutos);
        foreach (var nota in Trazabilidad) hash.Add(nota);
        return hash.ToHashCode();
    }
}
