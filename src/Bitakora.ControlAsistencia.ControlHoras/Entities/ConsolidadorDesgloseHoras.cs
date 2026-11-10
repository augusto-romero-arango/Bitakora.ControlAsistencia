using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Regla de producto: un retardo en cualquier franja se compensa con el excedente de cualquier otra
// del mismo dia. Los ultimos minutos del excedente (cronologicamente) compensan el retardo; los
// primeros sobreviven como extras genuinas. Aplica sobre todas las extras sin distincion de origen
// ni concepto; nunca toca descansos ni ordinarias.
public static class ConsolidadorDesgloseHoras
{
    public static DesgloseHoras Consolidar(
        IReadOnlyList<DesgloseFranja> desglosesFranja,
        int franjasAnomalas,
        bool sinCastigoPorRetardo = false)
    {
        // Copia mutable: la compensacion remueve o parte extras sin tocar el insumo original.
        var intervalosPorFranja = desglosesFranja
            .Select(franja => franja.Intervalos.ToList())
            .ToList();

        // Cada retardo neto ya descuenta la compensacion intra-franja.
        var retardoPendiente = sinCastigoPorRetardo
            ? 0
            : desglosesFranja.Sum(franja => franja.Retardo.RetardoNeto);

        IReadOnlyList<IntervaloTemporal> compensacionCrossFranja = retardoPendiente > 0
            ? ConsumirExtrasDesdeElFinal(intervalosPorFranja, retardoPendiente)
            : [];

        var retardoTotal = Retardo.Consolidar(
            desglosesFranja.Select(franja => franja.Retardo),
            compensacionCrossFranja);
        if (sinCastigoPorRetardo)
            retardoTotal = retardoTotal.SinCastigo();

        var desglosePorFranja = desglosesFranja
            .Select((franja, indice) => franja with { Intervalos = intervalosPorFranja[indice] })
            .ToList();

        return new DesgloseHoras(desglosePorFranja, retardoTotal, franjasAnomalas);
    }

    // Ordena explicitamente porque las franjas pueden llegar en cualquier orden. Muta
    // `intervalosPorFranja`: la extra consumida entera se remueve; la atravesada se reemplaza por su
    // parte izquierda (la extra genuina). Devuelve lo consumido en orden cronologico ascendente. Si
    // `minutos` excede las extras del dia, el sobrante queda como retardo neto sin compensar.
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
