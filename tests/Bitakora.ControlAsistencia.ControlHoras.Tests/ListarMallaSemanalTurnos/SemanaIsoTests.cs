using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarMallaSemanalTurnos;

public class SemanaIsoTests
{
    [Fact]
    public void De_DevuelveLunesADomingo_CuandoLaFechaEsMiercoles()
    {
        var semana = SemanaIso.De(new DateOnly(2027, 3, 3));

        semana.Lunes.Should().Be(new DateOnly(2027, 3, 1));
        semana.Domingo.Should().Be(new DateOnly(2027, 3, 7));
        semana.Anio.Should().Be(2027);
        semana.Numero.Should().Be(9);
    }

    [Fact]
    public void De_PerteneceALaSemanaDelLunesAnterior_CuandoLaFechaEsDomingo()
    {
        var semana = SemanaIso.De(new DateOnly(2027, 3, 7));

        semana.Lunes.Should().Be(new DateOnly(2027, 3, 1));
        semana.Domingo.Should().Be(new DateOnly(2027, 3, 7));
    }

    [Fact]
    public void De_UsaElAnioIso_CuandoLaSemanaCruzaElCambioDeAnio()
    {
        var semana = SemanaIso.De(new DateOnly(2027, 1, 1));

        semana.Lunes.Should().Be(new DateOnly(2026, 12, 28));
        semana.Anio.Should().Be(2026);
        semana.Numero.Should().Be(53);
    }
}
