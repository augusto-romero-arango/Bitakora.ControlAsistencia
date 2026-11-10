using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

public record ControlFranja(FranjaProgramada Programada, DateTime? Entrada, DateTime? Salida)
{
    public bool EsAnomala => Entrada is null || Salida is null;

    // Retorna null si la franja es anomala.
    public DesgloseFranja? CalcularDesglose(DateOnly fecha, Func<DateOnly, bool> esFestivo)
    {
        if (EsAnomala)
            return null;

        // Frontera unica DateTime -> MomentoDelDia: el DateTime no reaparece en el resto del metodo.
        var entradaMomento = MomentoDelDia.Desde(Entrada!.Value, fecha);
        var salidaMomento = MomentoDelDia.Desde(Salida!.Value, fecha);
        var trabajado = IntervaloTemporal.Crear(entradaMomento, salidaMomento);

        // Mutable: la compensacion consume desde el final sin romper el orden cronologico ascendente.
        var intervalos = ClasificadorTrabajo.Clasificar(Programada, trabajado, fecha, esFestivo).ToList();

        var inicioFranja = new MomentoDelDia(Programada.HoraInicio, 0);
        var finFranja = new MomentoDelDia(Programada.HoraFin, Programada.DiaOffsetFin);
        var retardo = Math.Max(0, entradaMomento.MinutosAbsolutos - inicioFranja.MinutosAbsolutos);
        var excedenteBruto = Math.Max(0, salidaMomento.MinutosAbsolutos - finFranja.MinutosAbsolutos);

        // Compensacion intra-franja: los ultimos min(retardo, excedente) minutos del excedente
        // compensan el retardo en vez de pagarse como extra.
        var compensacion = Math.Min(retardo, excedenteBruto);
        IReadOnlyList<IntervaloTemporal> tiempoCompensado = compensacion > 0
            ? ConsumirDesdeElFinal(intervalos, compensacion)
            : [];

        IReadOnlyList<IntervaloTemporal> tiempoRetardado = retardo > 0
            ? [IntervaloTemporal.Crear(inicioFranja, entradaMomento)]
            : [];

        return new DesgloseFranja(Programada, intervalos, Retardo.Crear(tiempoRetardado, tiempoCompensado));
    }

    // Devuelve lo consumido en orden cronologico ascendente. Muta `intervalos`: el consumido entero
    // se remueve; el atravesado se reemplaza por su parte izquierda. Precondicion: minutos <= suma de
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
