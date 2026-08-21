namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano del dia que viaja en DiaDepurado. Debe permanecer 100% primitivo: el modelo rico
// anterior (DesgloseHoras + DetalleControlFranja) dependia del resolver custom de Marten, que NO se
// aplica al canal de publicacion a Service Bus, y llegaba lossy al consumidor (field notes
// 2026-06-23). Con solo primitivos STJ lo (de)serializa nativo, sin ConfigurarSerializacion.
//
// Issue #424: HorasPorConcepto (ex MinutosPorConcepto) habla horas liquidables, no minutos -- la
// frontera de idiomas del BC pasa por aqui. Clave = Concepto.ToString() ("OrdinariaDiurna", ...) o la
// clave literal "Retardo"; valor = horas liquidables agregadas del dia para esa clave, producidas via
// HorasLiquidables (unico punto de conversion del BC). Todo el diccionario habla el mismo idioma,
// incluida la clave "Retardo".
// Trazabilidad: textos de auditoria del calculo -- sigue narrando en minutos/intervalos (memoria del
// calculo, no dato operable del mundo humano; decision explicita, no omision).
//
// Igualdad por valor de las colecciones via override manual de Equals/GetHashCode (precedente
// DetalleFranjaOrdinaria, #129): el record por defecto compara HorasPorConcepto/Trazabilidad por
// referencia. El override preserva la forma de record (constructor primario publico) y corrige el bug.
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
