using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class CorteEnTramosTests
{
    private static DateOnly D(string fecha) => DateOnly.ParseExact(fecha, "yyyy-MM-dd");

    [Fact]
    public void Cortar_DevuelveUnSoloTramo_CuandoElPeriodoCabeEnElMaximo()
    {
        var tramos = CorteEnTramos.Cortar(D("2026-09-01"), D("2026-09-30"), 366);

        tramos.Should().Equal(new TramoDeFechas(D("2026-09-01"), D("2026-09-30")));
    }

    [Fact]
    public void Cortar_DevuelveUnSoloTramo_CuandoElPeriodoDura366Dias()
    {
        var tramos = CorteEnTramos.Cortar(D("2026-01-01"), D("2027-01-01"), 366);

        tramos.Should().Equal(new TramoDeFechas(D("2026-01-01"), D("2027-01-01")));
    }

    [Fact]
    public void Cortar_DevuelveUnSoloTramo_CuandoDesdeEsIgualAHasta()
    {
        var tramos = CorteEnTramos.Cortar(D("2026-09-01"), D("2026-09-01"), 366);

        tramos.Should().Equal(new TramoDeFechas(D("2026-09-01"), D("2026-09-01")));
    }

    [Fact]
    public void Cortar_DevuelveTramosConsecutivosDeHasta366_CuandoElPeriodoPasaDelMaximo()
    {
        var tramos = CorteEnTramos.Cortar(D("2026-01-01"), D("2027-03-15"), 366);

        tramos.Should().Equal(
            new TramoDeFechas(D("2026-01-01"), D("2027-01-01")),
            new TramoDeFechas(D("2027-01-02"), D("2027-03-15")));
    }

    [Fact]
    public void Cortar_DevuelveDosTramosConElSegundoDeUnDia_CuandoElPeriodoDura367Dias()
    {
        var tramos = CorteEnTramos.Cortar(D("2026-01-01"), D("2027-01-02"), 366);

        tramos.Should().Equal(
            new TramoDeFechas(D("2026-01-01"), D("2027-01-01")),
            new TramoDeFechas(D("2027-01-02"), D("2027-01-02")));
    }

    [Fact]
    public void Cortar_DevuelveTresTramos_CuandoElPeriodoDuraMasDeDosMaximos()
    {
        var tramos = CorteEnTramos.Cortar(D("2026-01-01"), D("2026-01-10"), 4);

        tramos.Should().Equal(
            new TramoDeFechas(D("2026-01-01"), D("2026-01-04")),
            new TramoDeFechas(D("2026-01-05"), D("2026-01-08")),
            new TramoDeFechas(D("2026-01-09"), D("2026-01-10")));
    }
}
