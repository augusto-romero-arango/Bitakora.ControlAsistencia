using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasDelEquipo;

// CA-4: cota de 31 dias inclusive, recortada hacia adelante desde `desde`.
public class RangoConsultaTests
{
    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoEsExactamente31DiasInclusive()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 10, 31), false));
    }

    [Fact]
    public void Recortar_RecortaADesdeMasTreinta_CuandoElRangoSupera31Dias()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 10, 31), true));
    }

    [Fact]
    public void Recortar_RecortaUnSoloDia_CuandoElRangoTiene32Dias()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 10, 31), true));
    }

    [Fact]
    public void Recortar_DevuelveElMismoDia_CuandoDesdeYHastaCoinciden()
    {
        var dia = new DateOnly(2026, 10, 13);

        RangoConsulta.Recortar(dia, dia).Should().Be(new RangoAplicado(dia, false));
    }
}
