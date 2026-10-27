using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantilla;

public class AsignacionDeCicloTests
{
    private static string IdDelDia(int dia) => $"00000000-0000-7000-9000-00000000000{dia}";

    private static CuadroDeTurnos CicloDe(int dias) => new(
        "p", $"Ciclo de {dias}", dias, true,
        [.. Enumerable.Range(1, dias)
            .Select(d => new DiaDelCuadroDeTurnos(d, new TurnoDelCuadroDeTurnos(IdDelDia(d), $"Turno {d}", "(06:00-14:00)", true, false)))]);

    [Fact]
    public void DiaDelCiclo_EsElDiaUno_CuandoLaFechaEsLaFechaDeInicio()
    {
        var asignacion = AsignacionDeCiclo.Crear(CicloDe(8), new DateOnly(2027, 1, 3));

        asignacion.DiaDelCiclo(new DateOnly(2027, 1, 3)).Should().Be(1);
        asignacion.DiaDelCiclo(new DateOnly(2027, 1, 10)).Should().Be(8);
        asignacion.DiaDelCiclo(new DateOnly(2027, 1, 11)).Should().Be(1);
    }

    [Fact]
    public void DiaDelCiclo_EsElSexto_CuandoElInicioEsTresDeEneroYLaFechaEsPrimeroDeFebreroConCicloDeOcho()
    {
        var asignacion = AsignacionDeCiclo.Crear(CicloDe(8), new DateOnly(2027, 1, 3));

        asignacion.DiaDelCiclo(new DateOnly(2027, 2, 1)).Should().Be(6);
    }

    [Fact]
    public void Para_DevuelveElTurnoDelDiaDelCiclo_CuandoLaFechaDaVariasVueltas()
    {
        var asignacion = AsignacionDeCiclo.Crear(CicloDe(3), new DateOnly(2026, 9, 1));

        asignacion.Para(new DateOnly(2026, 9, 1)).Id.Should().Be(IdDelDia(1));
        asignacion.Para(new DateOnly(2026, 9, 2)).Id.Should().Be(IdDelDia(2));
        asignacion.Para(new DateOnly(2026, 9, 3)).Id.Should().Be(IdDelDia(3));
        asignacion.Para(new DateOnly(2026, 9, 4)).Id.Should().Be(IdDelDia(1));
        asignacion.Para(new DateOnly(2026, 9, 30)).Id.Should().Be(IdDelDia(3));
    }

    [Fact]
    public void Para_ContinuaElCicloHaciaAtras_CuandoLaFechaEsAnteriorAlInicio()
    {
        var asignacion = AsignacionDeCiclo.Crear(CicloDe(3), new DateOnly(2026, 9, 10));

        asignacion.DiaDelCiclo(new DateOnly(2026, 9, 9)).Should().Be(3);
        asignacion.Para(new DateOnly(2026, 9, 9)).Id.Should().Be(IdDelDia(3));
        asignacion.Para(new DateOnly(2026, 9, 7)).Id.Should().Be(IdDelDia(1));
        asignacion.Para(new DateOnly(2026, 9, 6)).Id.Should().Be(IdDelDia(3));
    }
}
