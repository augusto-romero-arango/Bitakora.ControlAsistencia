using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #115: Segmenta un IntervaloTemporal en sub-intervalos homogeneos cortando en las
// fronteras horarias legales (6AM, 7PM, medianoche). Logica pura sin estado.
// Diseno: trabaja sobre MomentoDelDia/IntervaloTemporal, nunca sobre DateTime.
// La conversion DateTime -> MomentoDelDia ocurre una sola vez en la frontera (#136).
public static class SegmentadorHorario
{
    private const int MinutosPorHora = 60;
    private const int MinutosPorDia = 1440;
    private const int MinutosFronteraMedianoche = 0;

    private static readonly int MinutosFronteraDiurna =
        FronterasHorariasLegales.InicioDiurna.Hour * MinutosPorHora
        + FronterasHorariasLegales.InicioDiurna.Minute;

    private static readonly int MinutosFronteraNocturna =
        FronterasHorariasLegales.InicioNocturna.Hour * MinutosPorHora
        + FronterasHorariasLegales.InicioNocturna.Minute;

    // Las tres fronteras horarias del dia (offsets en minutos desde medianoche).
    // Se reusan en cada llamada al barrer los dias del rango.
    private static readonly int[] OffsetsHorariosDelDia =
        [MinutosFronteraMedianoche, MinutosFronteraDiurna, MinutosFronteraNocturna];

    // Corta el intervalo en cada ocurrencia de las fronteras 6AM, 7PM y medianoche
    // dentro del rango (exclusivo de los extremos). Si no hay fronteras: retorna [intervalo].
    public static IReadOnlyList<IntervaloTemporal> Segmentar(IntervaloTemporal intervalo)
    {
        var fronteras = ObtenerFronterasInternas(intervalo);
        return fronteras.Count == 0
            ? [intervalo]
            : PartirEnFronteras(intervalo, fronteras);
    }

    // Genera los puntos de corte (en minutos absolutos) dentro del rango exclusivo del
    // intervalo: barre los dias diaInicio..diaFin, emite las tres fronteras de cada dia,
    // filtra las que caen estrictamente dentro del rango y las ordena ascendente.
    private static IReadOnlyList<int> ObtenerFronterasInternas(IntervaloTemporal intervalo)
    {
        var inicioMin = intervalo.MinutosAbsolutosInicio;
        var finMin = inicioMin + intervalo.DuracionEnMinutos;
        var diaInicio = inicioMin / MinutosPorDia;
        var diaFin = finMin / MinutosPorDia;

        return Enumerable.Range(diaInicio, diaFin - diaInicio + 1)
            .SelectMany(dia => OffsetsHorariosDelDia.Select(offset => dia * MinutosPorDia + offset))
            .Where(frontera => frontera > inicioMin && frontera < finMin)
            .OrderBy(frontera => frontera)
            .ToList();
    }

    // Aplica IntervaloTemporal.Partir secuencialmente en cada frontera. Cada Partir convierte
    // el punto de corte a MomentoDelDia internamente via DesdeMinutosAbsolutos (#143) y
    // construye los dos sub-intervalos. La duracion total se conserva por construccion.
    private static IReadOnlyList<IntervaloTemporal> PartirEnFronteras(
        IntervaloTemporal intervalo, IReadOnlyList<int> fronteras)
    {
        var resultado = new List<IntervaloTemporal>(fronteras.Count + 1);
        var actual = intervalo;
        foreach (var frontera in fronteras)
        {
            var (izq, der) = actual.Partir(frontera - actual.MinutosAbsolutosInicio);
            resultado.Add(izq);
            actual = der;
        }
        resultado.Add(actual);
        return resultado;
    }
}
