using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.SolicitarProgramacionTurno;

public class VentanaDeProgramacionTests
{
    [Fact]
    public void Crear_ConstruyeLaVentana_CuandoTieneExactamente31Dias()
    {
        var ventana = VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));

        ventana.ToString().Should().Be("2026-09-01 a 2026-10-01");
    }

    [Fact]
    public void Crear_Falla_CuandoTiene32Dias()
    {
        var act = () => VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2));

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{VentanaDeProgramacion.Mensajes.VentanaExcedeMaximo}*");
    }

    [Fact]
    public void Crear_Falla_CuandoDesdeEsPosteriorAHasta()
    {
        var act = () => VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{VentanaDeProgramacion.Mensajes.VentanaInvertida}*");
    }

    [Fact]
    public void DiasCubiertosPor_DevuelveTodaLaVentana_CuandoLaVigenciaEsAbiertaYAnteriorALaVentana()
    {
        var ventana = VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var dias = ventana.DiasCubiertosPor(new DateOnly(2025, 1, 1), null);

        dias.Should().Equal(Rango(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void DiasCubiertosPor_DevuelveSoloLosDiasDeLaVigencia_CuandoEstaCerradaYCabeDentroDeLaVentana()
    {
        var ventana = VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var dias = ventana.DiasCubiertosPor(new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 10));

        dias.Should().Equal(Rango(new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 10)));
    }

    [Fact]
    public void DiasCubiertosPor_DevuelveVacio_CuandoLaVigenciaTerminoAntesDeLaVentana()
    {
        var ventana = VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var dias = ventana.DiasCubiertosPor(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 30));

        dias.Should().BeEmpty();
    }

    [Fact]
    public void DiasCubiertosPor_RecortaDesdeElInicioDeLaVigencia_CuandoEmpiezaDentroDeLaVentana()
    {
        var ventana = VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var dias = ventana.DiasCubiertosPor(new DateOnly(2026, 9, 10), null);

        dias.Should().Equal(Rango(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void DiasCubiertosPor_RecortaHastaElFinDeLaVigencia_CuandoTerminaDentroDeLaVentana()
    {
        var ventana = VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var dias = ventana.DiasCubiertosPor(new DateOnly(2025, 1, 1), new DateOnly(2026, 9, 20));

        dias.Should().Equal(Rango(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20)));
    }

    // Oraculo independiente (MEF-ADR-0002): el rango esperado se arma sumando dias con
    // DateOnly.AddDays, nunca invocando DiasCubiertosPor sobre si mismo.
    private static IEnumerable<DateOnly> Rango(DateOnly desde, DateOnly hasta)
    {
        for (var dia = desde; dia <= hasta; dia = dia.AddDays(1))
            yield return dia;
    }
}
