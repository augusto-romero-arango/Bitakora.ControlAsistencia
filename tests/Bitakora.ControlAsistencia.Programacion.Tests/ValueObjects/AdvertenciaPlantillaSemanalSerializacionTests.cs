using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class AdvertenciaPlantillaSemanalSerializacionTests
{
    private static AdvertenciaPlantillaSemanal RoundTrip(AdvertenciaPlantillaSemanal advertencia)
    {
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(advertencia, opciones);
        return JsonSerializer.Deserialize<AdvertenciaPlantillaSemanal>(json, opciones)!;
    }

    [Fact]
    public void RoundTrip_ReconstruyePlantillaSinJornada()
    {
        var original = AdvertenciaPlantillaSemanal.PlantillaSinJornada();

        RoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_ReconstruyeAdvertenciaDeDia_ConMagnitud()
    {
        var original = AdvertenciaPlantillaSemanal.SuperaTopeDiario(2, DiaSemana.Jueves, 120);

        var restaurado = RoundTrip(original);

        restaurado.Should().Be(original);
        restaurado.Should().NotBe(AdvertenciaPlantillaSemanal.SuperaTopeDiario(2, DiaSemana.Viernes, 120));
    }

    [Fact]
    public void RoundTrip_ReconstruyeDiaSinTurno()
    {
        var original = AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Domingo);

        RoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_ReconstruyeAdvertenciaDeSemana_ConMagnitud()
    {
        var original = AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(3, 240);

        var restaurado = RoundTrip(original);

        restaurado.Should().Be(original);
        restaurado.Should().NotBe(AdvertenciaPlantillaSemanal.SuperaHorasSemanales(3, 240));
    }

    [Fact]
    public void RoundTrip_ReconstruyeAdvertenciaDeDescansos()
    {
        var original = AdvertenciaPlantillaSemanal.SobranDiasDeDescanso(1, 1);

        RoundTrip(original).Should().Be(original);
    }
}
