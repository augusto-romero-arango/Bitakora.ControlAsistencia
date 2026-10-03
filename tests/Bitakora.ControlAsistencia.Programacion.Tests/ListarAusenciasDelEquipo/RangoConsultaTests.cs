using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasDelEquipo;

// Cota local de 35 dias inclusive, recortada hacia adelante desde `desde`.
public class RangoConsultaTests
{
    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoTiene32DiasInclusive()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 7), new DateOnly(2026, 11, 7));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 7), false));
    }

    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoTiene35DiasInclusive()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 7), new DateOnly(2026, 11, 10));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 10), false));
    }

    [Fact]
    public void Recortar_RecortaUnSoloDia_CuandoElRangoTiene36Dias()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 7), new DateOnly(2026, 11, 11));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 10), true));
    }

    [Fact]
    public void Recortar_RecortaHaciaAdelanteDesdeElInicio_CuandoElRangoExcedeAmpliamenteLaCota()
    {
        var resultado = RangoConsulta.Recortar(new DateOnly(2026, 10, 7), new DateOnly(2026, 12, 31));

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 10), true));
    }

    [Fact]
    public void Recortar_DevuelveElMismoDia_CuandoDesdeYHastaCoinciden()
    {
        var dia = new DateOnly(2026, 10, 13);

        RangoConsulta.Recortar(dia, dia).Should().Be(new RangoAplicado(dia, false));
    }
}
