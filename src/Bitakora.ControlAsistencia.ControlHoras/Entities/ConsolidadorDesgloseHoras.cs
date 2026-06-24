using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #116: Consolida los DesgloseFranja de un dia operativo en un DesgloseHoras,
// aplicando compensacion cronologica inversa cross-franja sobre las extras de todas
// las franjas. Clase estatica pura: sin interaccion de aggregate ni event sourcing.
//
// Regla de producto: un retardo en cualquier franja se compensa con el excedente de
// cualquier otra del mismo dia. Los ultimos minutos del excedente (cronologicamente)
// compensan el retardo; los primeros minutos sobreviven como extras genuinas. La
// compensacion aplica sobre TODAS las extras sin distincion de origen ni concepto;
// nunca toca descansos ni ordinarias.
//
// La integracion reactiva al aggregate (ControlDiarioAggregateRoot) se hace en #139.
public static class ConsolidadorDesgloseHoras
{
    public static DesgloseHoras Consolidar(
        IReadOnlyList<DesgloseFranja> desglosesFranja,
        int franjasAnomalas)
    {
        // Copia mutable de los intervalos de cada franja: la compensacion cross-franja consume
        // extras desde el final, removiendo o partiendo intervalos sin tocar el insumo original.
        var intervalosPorFranja = desglosesFranja
            .Select(franja => franja.Intervalos.ToList())
            .ToList();

        // Retardo pendiente del dia = suma de los retardos netos de cada franja (cada uno ya
        // descontada su compensacion intra-franja, calculada por separado en #136).
        var retardoPendiente = desglosesFranja.Sum(franja => franja.Retardo.RetardoNeto);

        IReadOnlyList<IntervaloTemporal> compensacionCrossFranja = retardoPendiente > 0
            ? ConsumirExtrasDesdeElFinal(intervalosPorFranja, retardoPendiente)
            : [];

        // El retardo del dia acumula el retardado/compensado de cada franja mas el compensado
        // cross-franja recien consumido. Retardo lo consolida sin exponer sus intervalos.
        var retardoTotal = Retardo.Consolidar(
            desglosesFranja.Select(franja => franja.Retardo),
            compensacionCrossFranja);

        var desglosePorFranja = desglosesFranja
            .Select((franja, indice) => franja with { Intervalos = intervalosPorFranja[indice] })
            .ToList();

        return new DesgloseHoras(desglosePorFranja, retardoTotal, franjasAnomalas);
    }

    // Recolecta las extras de todas las franjas, las ordena cronologicamente de forma EXPLICITA
    // (las franjas pueden llegar en cualquier orden) y consume `minutos` desde el final segun la
    // regla cronologica inversa: los ultimos minutos del excedente compensan el retardo. Muta
    // `intervalosPorFranja`: la extra consumida por completo se remueve de su franja; la atravesada
    // parcialmente se reemplaza por su parte izquierda (la extra genuina visible). Devuelve los
    // IntervaloTemporal consumidos en orden cronologico ascendente, para el TiempoCompensado del
    // Retardo. Si `minutos` excede la suma de las extras del dia, consume todas las extras
    // disponibles y el sobrante queda como retardo neto sin compensar (caso CA-3: retardo sin
    // excedente que lo cubra). El metodo no exige `minutos` <= extras: tolera el deficit.
    private static IReadOnlyList<IntervaloTemporal> ConsumirExtrasDesdeElFinal(
        List<List<IntervaloClasificado>> intervalosPorFranja, int minutos)
    {
        var extrasDescendente = intervalosPorFranja
            .SelectMany((intervalos, franja) => intervalos
                .Where(intervalo => EsExtra(intervalo.Concepto))
                .Select(intervalo => (franja, extra: intervalo)))
            .OrderByDescending(item => item.extra.Intervalo)
            .ToList();

        var consumidos = new List<IntervaloTemporal>();
        var pendientes = minutos;
        foreach (var (franja, extra) in extrasDescendente)
        {
            if (pendientes == 0)
                break;

            var lista = intervalosPorFranja[franja];
            var indice = lista.IndexOf(extra);
            if (extra.DuracionEnMinutos <= pendientes)
            {
                consumidos.Add(extra.Intervalo);
                lista.RemoveAt(indice);
                pendientes -= extra.DuracionEnMinutos;
            }
            else
            {
                var (izquierdo, derecho) = extra.Intervalo.Partir(extra.DuracionEnMinutos - pendientes);
                lista[indice] = new IntervaloClasificado(izquierdo, extra.Concepto);
                consumidos.Add(derecho);
                pendientes = 0;
            }
        }

        consumidos.Reverse();
        return consumidos;
    }

    // Una extra es cualquier hora extra, sin distincion de banda ni tipo de dia: compensa el retardo
    // por igual. Las ordinarias, las dominicales/festivas ordinarias y los descansos nunca se consumen.
    private static bool EsExtra(Concepto concepto) =>
        concepto is Concepto.ExtraDiurna
            or Concepto.ExtraNocturna
            or Concepto.ExtraDiurnaDominicalFestiva
            or Concepto.ExtraNocturnaDominicalFestiva;
}
