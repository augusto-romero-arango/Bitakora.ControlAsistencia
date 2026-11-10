// Banda diurna [06:00, 19:00): 06:00 inclusivo, 19:00 exclusivo.
// Precondicion de todos los tests: el intervalo es homogeneo (no cruza fronteras de banda).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ClasificadorHorarioBandaTests
{
    private static IntervaloTemporal Intervalo(int horaInicio, int minInicio, int horaFin, int minFin, int diaOffset = 0) =>
        IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(horaInicio, minInicio), diaOffset),
            new MomentoDelDia(new TimeOnly(horaFin, minFin), diaOffset));

    [Fact]
    public void ClasificarBanda_RetornaDiurna_CuandoIntervaloEntre08hY12h()
    {
        var intervalo = Intervalo(8, 0, 12, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Diurna);
    }

    [Fact]
    public void ClasificarBanda_RetornaNocturna_CuandoIntervaloEntre20hY23h()
    {
        var intervalo = Intervalo(20, 0, 23, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Nocturna);
    }

    [Fact]
    public void ClasificarBanda_RetornaNocturna_CuandoIntervaloEsMadrugadaEntre00h30Y05h30()
    {
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(0, 30), 1),
            new MomentoDelDia(new TimeOnly(5, 30), 1));

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Nocturna);
    }

    [Fact]
    public void ClasificarBanda_RetornaDiurna_CuandoIntervaloEntre18hY19h()
    {
        var intervalo = Intervalo(18, 0, 19, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Diurna);
    }

    [Fact]
    public void ClasificarBanda_RetornaDiurna_CuandoIntervaloIniciaExactamenteEn06h()
    {
        // Pinea el operador >= InicioDiurna contra una regresion a >.
        var intervalo = Intervalo(6, 0, 10, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Diurna);
    }

    [Fact]
    public void ClasificarBanda_RetornaNocturna_CuandoIntervaloIniciaExactamenteEn19h()
    {
        // Pinea el operador < InicioNocturna contra una regresion a <=.
        var intervalo = Intervalo(19, 0, 22, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Nocturna);
    }
}
