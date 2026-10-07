using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class ExposicionValoresTests
{
    [Fact]
    public void Crear_ExponeHorasYMinutos_CuandoTraeAmbos()
    {
        var valor = HorasYMinutos.Crear(8, 12);

        valor.Horas.Should().Be(8);
        valor.Minutos.Should().Be(12);
    }

    [Fact]
    public void Crear_ExponeCeros_CuandoEsCero()
    {
        var valor = HorasYMinutos.Crear(0, 0);

        valor.Horas.Should().Be(0);
        valor.Minutos.Should().Be(0);
    }

    [Fact]
    public void Crear_ExponeLosCuatroValores_CuandoSeCreanLosLimites()
    {
        var limites = LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 30), HorasYMinutos.Crear(8, 12), HorasYMinutos.Crear(2, 5), 1);

        limites.HorasSemanales.Should().Be(HorasYMinutos.Crear(42, 30));
        limites.TopeDiario.Should().Be(HorasYMinutos.Crear(8, 12));
        limites.MinimoDiario.Should().Be(HorasYMinutos.Crear(2, 5));
        limites.DiasDescansoPorSemana.Should().Be(1);
    }
}
