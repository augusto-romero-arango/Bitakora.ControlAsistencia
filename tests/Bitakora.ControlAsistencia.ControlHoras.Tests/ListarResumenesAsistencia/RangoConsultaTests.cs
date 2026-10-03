// Duplicado a proposito de los RangoConsultaTests de ListarTurnosVigentes/ListarAsistenciasDiarias
// -- ver el comentario de clase de RangoConsulta.cs en este feature folder. Cada oraculo se arma a
// mano (MEF-ADR-0002): nunca se deriva ejecutando Recortar sobre si mismo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarResumenesAsistencia;

public class RangoConsultaTests
{
    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoEstaDentroDeLaCotaDe35Dias()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 10);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 8, 10), false));
    }

    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoEsExactamente32DiasInclusive()
    {
        var desde = new DateOnly(2026, 10, 7);
        var hasta = new DateOnly(2026, 11, 7);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 7), false));
    }

    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoEsExactamente35DiasSinAlineacionSemanal()
    {
        var desde = new DateOnly(2026, 10, 7);
        var hasta = new DateOnly(2026, 11, 10);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 10), false));
    }

    [Fact]
    public void Recortar_RecortaHaciaAdelanteDesdeDesde_CuandoElRangoExcedeLargamenteLaCotaDe35Dias()
    {
        var desde = new DateOnly(2026, 10, 7);
        var hasta = new DateOnly(2027, 3, 31);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 10), true));
    }

    [Fact]
    public void Recortar_RecortaUnSoloDiaDeExceso_CuandoElRangoEsDe36DiasInclusive()
    {
        var desde = new DateOnly(2026, 10, 7);
        var hasta = new DateOnly(2026, 11, 11); // 36 dias inclusive: un dia por encima de la cota

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 11, 10), true));
    }

    [Fact]
    public void Recortar_DevuelveElMismoDia_CuandoDesdeYHastaCoinciden()
    {
        var desde = new DateOnly(2026, 8, 5);

        var resultado = RangoConsulta.Recortar(desde, desde);

        resultado.Should().Be(new RangoAplicado(desde, false));
    }
}
