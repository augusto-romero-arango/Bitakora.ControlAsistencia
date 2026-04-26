// Issue #115: Segmentar intervalo trabajado por fronteras horarias.
// Tests del metodo IntervaloTemporal.Segmentar - operacion geometrica pura.
// Las fronteras se pasan como literales TimeOnly (medianoche, 6AM, 7PM): el VO no
// conoce leyes colombianas. El conocimiento legal vive en
// ControlHoras.Entities.FronterasHorariasLegales.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

public class IntervaloTemporalSegmentacionTests
{
    private static readonly IReadOnlyList<TimeOnly> FronterasLegales =
        [TimeOnly.MinValue, new(6, 0), new(19, 0)];

    private static MomentoDelDia En(int hora, int minuto, int diaOffset = 0) =>
        new(new TimeOnly(hora, minuto), diaOffset);

    private static IntervaloTemporal Intervalo(MomentoDelDia inicio, MomentoDelDia fin) =>
        IntervaloTemporal.Crear(inicio, fin);

    [Fact]
    public void Segmentar_RetornaUnElemento_CuandoNoHayFronterasEnElRango()
    {
        // CA-1: 08:00-12:00 diurno - sin cruzar ninguna frontera (6AM queda antes, 7PM queda despues)
        var intervalo = Intervalo(En(8, 0), En(12, 0));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(1);
        resultado[0].Should().Be(intervalo);
    }

    [Fact]
    public void Segmentar_RetornaDosSubintervalos_CuandoCruzaLas19h()
    {
        // CA-2: 14:00-22:00 cruza la frontera de 19:00 -> [14:00-19:00, 19:00-22:00]
        var intervalo = Intervalo(En(14, 0), En(22, 0));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(2);
        resultado[0].Should().Be(Intervalo(En(14, 0), En(19, 0)));
        resultado[1].Should().Be(Intervalo(En(19, 0), En(22, 0)));
    }

    [Fact]
    public void Segmentar_RetornaDosSubintervalos_CuandoCruzaMedianoche()
    {
        // CA-3: 22:00-02:00+1 cruza la medianoche -> [22:00-00:00+1, 00:00+1-02:00+1]
        var intervalo = Intervalo(En(22, 0), En(2, 0, 1));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(2);
        resultado[0].Should().Be(Intervalo(En(22, 0), En(0, 0, 1)));
        resultado[1].Should().Be(Intervalo(En(0, 0, 1), En(2, 0, 1)));
    }

    [Fact]
    public void Segmentar_RetornaTresSubintervalos_CuandoCruzaMedianocheY6hDelDiaSiguiente()
    {
        // CA-4: 22:00-08:00+1 cruza medianoche y 06:00+1 -> [22:00-00:00+1, 00:00+1-06:00+1, 06:00+1-08:00+1]
        var intervalo = Intervalo(En(22, 0), En(8, 0, 1));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(3);
        resultado[0].Should().Be(Intervalo(En(22, 0), En(0, 0, 1)));
        resultado[1].Should().Be(Intervalo(En(0, 0, 1), En(6, 0, 1)));
        resultado[2].Should().Be(Intervalo(En(6, 0, 1), En(8, 0, 1)));
    }

    [Fact]
    public void Segmentar_RetornaTresSubintervalos_CuandoCruzaLas19hYMedianoche()
    {
        // CA-5: 18:00-01:00+1 cruza 19:00 y medianoche -> [18:00-19:00, 19:00-00:00+1, 00:00+1-01:00+1]
        var intervalo = Intervalo(En(18, 0), En(1, 0, 1));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(3);
        resultado[0].Should().Be(Intervalo(En(18, 0), En(19, 0)));
        resultado[1].Should().Be(Intervalo(En(19, 0), En(0, 0, 1)));
        resultado[2].Should().Be(Intervalo(En(0, 0, 1), En(1, 0, 1)));
    }

    [Fact]
    public void Segmentar_RetornaUnElemento_CuandoIniciaJustoEnFronteraDeLas19h()
    {
        // CA-6: 19:00-22:00 - el inicio coincide exactamente con la frontera (corte exclusivo)
        var intervalo = Intervalo(En(19, 0), En(22, 0));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(1);
        resultado[0].Should().Be(intervalo);
    }

    [Fact]
    public void Segmentar_RetornaUnElemento_CuandoTerminaJustoEnFronteraDeLas19h()
    {
        // CA-7: 14:00-19:00 - el fin coincide exactamente con la frontera (corte exclusivo)
        var intervalo = Intervalo(En(14, 0), En(19, 0));

        var resultado = intervalo.Segmentar(FronterasLegales);

        resultado.Should().HaveCount(1);
        resultado[0].Should().Be(intervalo);
    }

    [Fact]
    public void Segmentar_SumaDuracionesIgualaDuracionOriginal_CuandoIntervaloCruzaVariasFronteras()
    {
        // CA-8: invariante de cobertura - sin huecos ni traslapes.
        // Caso: 22:00-08:00+1 produce 3 sub-intervalos (120 + 360 + 120 = 600 min).
        var intervalo = Intervalo(En(22, 0), En(8, 0, 1));

        var resultado = intervalo.Segmentar(FronterasLegales);

        var sumaDuraciones = resultado.Sum(i => i.DuracionEnMinutos);
        sumaDuraciones.Should().Be(intervalo.DuracionEnMinutos);
    }
}
