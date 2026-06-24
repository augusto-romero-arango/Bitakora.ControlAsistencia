using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-122: Value object que representa el resultado de depurar marcaciones contra una franja ordinaria.
// No viaja entre dominios - vive en ControlHoras junto al aggregate que lo usa.
// Issue #183: el dia cruza la frontera ya discriminado en HorasDiscriminadas (payload primitivo),
// no como detalle de franja; el manejo de anomalias queda diferido al flujo de aprobacion.
public record ControlFranja(DetalleFranjaOrdinaria Programada, DateTime? Entrada, DateTime? Salida)
{
    // CA-9: propiedad calculada; anomala cuando falta Entrada o Salida
    public bool EsAnomala => Entrada is null || Salida is null;

    // HU-136: capa 4 (final) del desglose de franja. Frontera unica DateTime -> MomentoDelDia.
    // Integra la lista base de ClasificadorTrabajo (#135) con retardo, excedente bruto y
    // compensacion intra-franja. Retorna null si la franja es anomala (entrada o salida nula).
    public DesgloseFranja? CalcularDesglose(DateOnly fecha, Func<DateOnly, bool> esFestivo)
    {
        if (EsAnomala)
            return null;

        // Frontera unica DateTime -> MomentoDelDia: a partir de aqui todo es MomentoDelDia /
        // IntervaloTemporal. El DateTime no reaparece en el resto del metodo (#143).
        var entradaMomento = MomentoDelDia.Desde(Entrada!.Value, fecha);
        var salidaMomento = MomentoDelDia.Desde(Salida!.Value, fecha);
        var trabajado = IntervaloTemporal.Crear(entradaMomento, salidaMomento);

        // Lista base: cada minuto trabajado con su concepto legal, ya recortado a la entrada
        // efectiva y segmentado por banda/tipo de dia/programacion (#135). Mutable para aplicar
        // la compensacion consumiendo desde el final sin romper el orden cronologico ascendente.
        var intervalos = ClasificadorTrabajo.Clasificar(Programada, trabajado, fecha, esFestivo).ToList();

        // Retardo y excedente bruto se calculan mirando la franja completa contra lo programado.
        var inicioFranja = new MomentoDelDia(Programada.HoraInicio, 0);
        var finFranja = new MomentoDelDia(Programada.HoraFin, Programada.DiaOffsetFin);
        var retardo = Math.Max(0, entradaMomento.MinutosAbsolutos - inicioFranja.MinutosAbsolutos);
        var excedenteBruto = Math.Max(0, salidaMomento.MinutosAbsolutos - finFranja.MinutosAbsolutos);

        // Compensacion intra-franja: los ultimos N minutos del excedente (N = min(retardo, excedente))
        // compensan el retardo en vez de pagarse como extra. Consistente con la regla cronologica
        // inversa de #116. Cuando no hay ambos, N = 0 y la lista base queda intacta.
        var compensacion = Math.Min(retardo, excedenteBruto);
        IReadOnlyList<IntervaloTemporal> tiempoCompensado = compensacion > 0
            ? ConsumirDesdeElFinal(intervalos, compensacion)
            : [];

        IReadOnlyList<IntervaloTemporal> tiempoRetardado = retardo > 0
            ? [IntervaloTemporal.Crear(inicioFranja, entradaMomento)]
            : [];

        return new DesgloseFranja(Programada, intervalos, DetalleRetardo.Crear(tiempoRetardado, tiempoCompensado));
    }

    // Consume los ultimos `minutos` de la lista clasificada recorriendola en orden cronologico
    // inverso, y devuelve los IntervaloTemporal concretos consumidos en orden cronologico ascendente
    // (para el TiempoCompensado del DetalleRetardo). Muta `intervalos`: el intervalo consumido por
    // completo se remueve; el que se atraviesa parcialmente se reemplaza por su parte izquierda
    // (visible) mientras la derecha pasa a la compensacion. Precondicion: minutos <= suma de
    // duraciones de la lista (garantizada por compensacion <= excedenteBruto <= tiempo trabajado).
    private static IReadOnlyList<IntervaloTemporal> ConsumirDesdeElFinal(
        List<IntervaloClasificado> intervalos, int minutos)
    {
        var consumidos = new List<IntervaloTemporal>();
        var pendientes = minutos;
        while (pendientes > 0)
        {
            var indice = intervalos.Count - 1;
            var ultimo = intervalos[indice];
            if (ultimo.DuracionEnMinutos <= pendientes)
            {
                consumidos.Add(ultimo.Intervalo);
                intervalos.RemoveAt(indice);
                pendientes -= ultimo.DuracionEnMinutos;
            }
            else
            {
                var (izquierdo, derecho) = ultimo.Intervalo.Partir(ultimo.DuracionEnMinutos - pendientes);
                intervalos[indice] = new IntervaloClasificado(izquierdo, ultimo.Concepto);
                consumidos.Add(derecho);
                pendientes = 0;
            }
        }

        consumidos.Reverse();
        return consumidos;
    }
}
