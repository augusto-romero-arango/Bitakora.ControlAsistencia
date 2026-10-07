using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoMinutosOrdinariosTests
{
    [Fact]
    public void MinutosOrdinarios_DescuentaDescansos_CuandoLaFranjaTieneDescanso()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(17, 0))
            .ConDescanso(new TimeOnly(12, 0), new TimeOnly(13, 0));

        Turno.Crear("Oficina", false, [franja]).MinutosOrdinarios().Should().Be(480);
    }

    [Fact]
    public void MinutosOrdinarios_CuentaCompletoEnElDiaQueEmpieza_CuandoCruzaLaMedianoche()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        Turno.Crear("Noche", false, [franja]).MinutosOrdinarios().Should().Be(480);
    }

    [Fact]
    public void MinutosOrdinarios_DescuentaExtras_CuandoLaFranjaTieneExtra()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(18, 0))
            .ConExtra(new TimeOnly(16, 0), new TimeOnly(18, 0));

        Turno.Crear("Largo", false, [franja]).MinutosOrdinarios().Should().Be(600);
    }

    [Fact]
    public void MinutosOrdinarios_SumaLasFranjas_CuandoTieneDos()
    {
        var turno = Turno.Crear("Partido", false,
        [
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(10, 0)),
            FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(18, 0))
        ]);

        turno.MinutosOrdinarios().Should().Be(480);
    }

    [Fact]
    public void MinutosOrdinarios_EsCero_CuandoEsDescanso()
    {
        Turno.Crear("Libre", true, []).MinutosOrdinarios().Should().Be(0);
    }

    [Fact]
    public void MinutosOrdinarios_EsCero_CuandoEstaIncompleto()
    {
        Turno.Crear("Nuevo", false, []).MinutosOrdinarios().Should().Be(0);
    }

    [Fact]
    public void FranjaMinutosOrdinarios_RestaDescansosYExtras_CuandoTieneAmbos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(18, 0))
            .ConDescanso(new TimeOnly(10, 0), new TimeOnly(10, 30))
            .ConExtra(new TimeOnly(16, 0), new TimeOnly(18, 0));

        franja.MinutosOrdinarios().Should().Be(570);
    }
}
