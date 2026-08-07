// Issue #329 CA-3: logica pura de recorte del rango de ListarTurnosVigentes. Sin Marten, sin
// Postgres, sin QuerySession -- funciona sobre DateOnly (skills/projections/read-apis.md: la cota
// y el recorte son logica de la Function, no de la proyeccion, que este issue no toca). Cada
// oraculo se arma a mano (MEF-ADR-0002): nunca se deriva ejecutando RangoConsulta.Recortar sobre
// si mismo.
//
// Agregado en la revision: la fase verde duplico RangoConsulta desde ListarTurnosDiarios (#290)
// sin duplicar sus tests, asi que CA-3 quedaba cubierto UNICAMENTE por el smoke test contra dev
// -- que exige deploy y no corre en el CI del PR. Espejo de
// ListarTurnosDiarios/RangoConsultaTests.cs: mientras las dos copias de la logica convivan
// (MEF-ADR-0018, Rule of Three), cada una necesita su propia guarda -- si no, una divergencia
// silenciosa entre ambas cotas no la detecta nadie.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

public class RangoConsultaTests
{
    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoEstaDentroDeLaCotaDe31Dias()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 10);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 8, 10), false));
    }

    [Fact]
    public void Recortar_DevuelveHastaSinCambios_CuandoElRangoEsExactamente31DiasInclusive()
    {
        // CA-3: 31 dias inclusive (desde + 30 dias) es el limite exacto de la cota, no la excede.
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 31);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 8, 31), false));
    }

    [Fact]
    public void Recortar_RecortaHaciaAdelanteDesdeDesde_CuandoElRangoExcedeLargamenteLaCotaDe31Dias()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 12, 31);

        var resultado = RangoConsulta.Recortar(desde, hasta);

        // CA-3: hastaAplicado = desde + 30 dias (31 dias inclusive), rangoRecortado: true. El
        // recorte es hacia ADELANTE desde `desde` -- nunca hacia atras desde `hasta`, y nunca
        // relativo a la fecha de hoy.
        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 8, 31), true));
    }

    [Fact]
    public void Recortar_RecortaUnSoloDiaDeExceso_CuandoElRangoSuperaLaCotaPorUnSoloDia()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 9, 1); // 32 dias inclusive: un dia por encima de la cota

        var resultado = RangoConsulta.Recortar(desde, hasta);

        resultado.Should().Be(new RangoAplicado(new DateOnly(2026, 8, 31), true));
    }

    [Fact]
    public void Recortar_DevuelveElMismoDia_CuandoDesdeYHastaCoinciden()
    {
        // Consulta de un solo dia (desde == hasta): el panorama del programador para una fecha
        // puntual, forma que el smoke test usa como sensor de materializacion.
        var desde = new DateOnly(2026, 8, 5);

        var resultado = RangoConsulta.Recortar(desde, desde);

        resultado.Should().Be(new RangoAplicado(desde, false));
    }
}
