// Issue #427 CA-3: logica pura de recorte del rango de ListarAsistenciasDiarias. Sin Marten, sin
// Postgres, sin QuerySession -- funciona sobre DateOnly (skills/projections/read-apis.md: la cota
// y el recorte son logica de la Function, no de la proyeccion, que este issue no toca). Cada
// oraculo se arma a mano (MEF-ADR-0002): nunca se deriva ejecutando RangoConsulta.Recortar sobre
// si mismo.
//
// Duplicado a proposito del RangoConsultaTests de ListarTurnosVigentes (issue #329) -- SEGUNDA
// aparicion de esta politica en el dominio, tolerada bajo Rule of Three (MEF-ADR-0018; ver
// RangoConsulta.cs de este feature folder para el razonamiento completo).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAsistenciasDiarias;

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
        // Consulta de un solo dia (desde == hasta): la pantalla del Aprobador para una fecha
        // puntual.
        var desde = new DateOnly(2026, 8, 5);

        var resultado = RangoConsulta.Recortar(desde, desde);

        resultado.Should().Be(new RangoAplicado(desde, false));
    }
}
