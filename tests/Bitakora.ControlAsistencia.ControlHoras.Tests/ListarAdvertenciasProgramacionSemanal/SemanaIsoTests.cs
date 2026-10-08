using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAdvertenciasProgramacionSemanal;

public class SemanaIsoTests
{
    [Fact]
    public void De_UbicaLaSemana41_CuandoLaFechaEsMiercoles()
    {
        var semana = SemanaIso.De(new DateOnly(2026, 10, 7));

        semana.Should().Be(new SemanaIso(2026, 41, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11)));
    }

    [Fact]
    public void De_UbicaLaSemana53DeAnioAnterior_CuandoLaFechaEs2027_01_03()
    {
        var semana = SemanaIso.De(new DateOnly(2027, 1, 3));

        semana.Should().Be(new SemanaIso(2026, 53, new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3)));
    }

    [Fact]
    public void De_DevuelveLaMismaSemana_CuandoLaFechaEsLunesOEsDomingo()
    {
        SemanaIso.De(new DateOnly(2026, 10, 5)).Numero.Should().Be(41);
        SemanaIso.De(new DateOnly(2026, 10, 11)).Numero.Should().Be(41);
    }
}
