using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class AdvertenciaPlantillaTurnosTests
{
    private static AdvertenciaPlantillaTurnos RoundTrip(AdvertenciaPlantillaTurnos advertencia)
    {
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(advertencia, opciones);
        return JsonSerializer.Deserialize<AdvertenciaPlantillaTurnos>(json, opciones)!;
    }

    [Fact]
    public void PlantillaSinJornada_NoTieneDiaNiMagnitud()
    {
        var advertencia = AdvertenciaPlantillaTurnos.PlantillaSinJornada();

        advertencia.Tipo.Should().Be(TipoAdvertenciaPlantilla.PlantillaSinJornada);
        advertencia.Dia.Should().BeNull();
        advertencia.Magnitud.Should().Be(0);
    }

    [Fact]
    public void SuperaTopeDiario_ExponeTipoDiaYMagnitud()
    {
        var advertencia = AdvertenciaPlantillaTurnos.SuperaTopeDiario(5, 90);

        advertencia.Tipo.Should().Be(TipoAdvertenciaPlantilla.SuperaTopeDiario);
        advertencia.Dia.Should().Be(5);
        advertencia.Magnitud.Should().Be(90);
    }

    [Fact]
    public void Igualdad_ComparaPorValor()
    {
        AdvertenciaPlantillaTurnos.DiaSinTurno(3).Should().Be(AdvertenciaPlantillaTurnos.DiaSinTurno(3));
        AdvertenciaPlantillaTurnos.DiaSinTurno(3).Should().NotBe(AdvertenciaPlantillaTurnos.DiaSinTurno(4));
        AdvertenciaPlantillaTurnos.SuperaTopeDiario(3, 10).Should()
            .NotBe(AdvertenciaPlantillaTurnos.SuperaTopeDiario(3, 20));
        AdvertenciaPlantillaTurnos.DiaSinTurno(3).GetHashCode().Should()
            .Be(AdvertenciaPlantillaTurnos.DiaSinTurno(3).GetHashCode());
    }

    [Fact]
    public void Orden_PoneLaDePlantillaPrimeroYLuegoPorDiaYTipo()
    {
        var desordenadas = new[]
        {
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(2, 10),
            AdvertenciaPlantillaTurnos.DiaSinTurno(2),
            AdvertenciaPlantillaTurnos.DiaSinTurno(1),
            AdvertenciaPlantillaTurnos.PlantillaSinJornada()
        };

        desordenadas.Order().Should().Equal(
            AdvertenciaPlantillaTurnos.PlantillaSinJornada(),
            AdvertenciaPlantillaTurnos.DiaSinTurno(1),
            AdvertenciaPlantillaTurnos.DiaSinTurno(2),
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(2, 10));
    }

    [Fact]
    public void RoundTrip_ReconstruyeTipoDiaYMagnitud()
    {
        var original = AdvertenciaPlantillaTurnos.PorDebajoDelMinimoDiario(6, 45);

        var restaurado = RoundTrip(original);

        restaurado.Should().Be(original);
        restaurado.Tipo.Should().Be(TipoAdvertenciaPlantilla.PorDebajoDelMinimoDiario);
        restaurado.Dia.Should().Be(6);
        restaurado.Magnitud.Should().Be(45);
    }

    [Fact]
    public void RoundTrip_ReconstruyePlantillaSinJornada()
    {
        RoundTrip(AdvertenciaPlantillaTurnos.PlantillaSinJornada())
            .Should().Be(AdvertenciaPlantillaTurnos.PlantillaSinJornada());
    }
}
