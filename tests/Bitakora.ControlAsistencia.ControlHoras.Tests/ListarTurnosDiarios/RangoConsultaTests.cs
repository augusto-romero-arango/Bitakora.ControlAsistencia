// Issue #290 CA-3/CA-4: logica pura de recorte del rango de ListarTurnosDiarios. Sin Marten, sin
// Postgres, sin QuerySession -- funciona sobre DateOnly (skills/projections/read-apis.md: la cota
// y el recorte son logica de la Function, no de la proyeccion, que no cambia en este issue). Cada
// oraculo se arma a mano (MEF-ADR-0002): nunca se deriva ejecutando RangoConsulta.Recortar sobre
// si mismo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosDiarios;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosDiarios;

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
        // CA-4: 31 dias inclusive (desde + 30 dias) es el limite exacto de la cota, no la excede.
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
        // recorte es hacia ADELANTE desde `desde` -- nunca hacia atras desde `hasta`.
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
    public void Recortar_DevuelveElMismoDia_CuandoDesdeYHastaCoincidenFormaUnDiaTodos()
    {
        // Forma (c) del issue: "quien trabaja hoy y en que turno" -- desde == hasta.
        var desde = new DateOnly(2026, 8, 5);

        var resultado = RangoConsulta.Recortar(desde, desde);

        resultado.Should().Be(new RangoAplicado(desde, false));
    }
}
