using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class AdvertenciasDePlantillaDeTurnosCalculadasSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001292");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConAdvertenciasCompletas()
    {
        var advertencias = new[]
        {
            AdvertenciaPlantillaTurnos.DiaSinTurno(1),
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(2, 30),
            AdvertenciaPlantillaTurnos.PorDebajoDelMinimoDiario(3, 90)
        };
        var evento = AdvertenciasDePlantillaDeTurnosCalculadas.Crear(PlantillaId, advertencias);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<AdvertenciasDePlantillaDeTurnosCalculadas>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
        restaurado.Advertencias.Should().Equal(advertencias);
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConPlantillaSinJornada()
    {
        var evento = AdvertenciasDePlantillaDeTurnosCalculadas.Crear(
            PlantillaId, [AdvertenciaPlantillaTurnos.PlantillaSinJornada()]);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<AdvertenciasDePlantillaDeTurnosCalculadas>(json, opciones);

        restaurado!.Advertencias.Should().Equal(AdvertenciaPlantillaTurnos.PlantillaSinJornada());
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDelEvento()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            AdvertenciasDePlantillaDeTurnosCalculadas.Crear(PlantillaId, []), opciones);

        var act = () => JsonSerializer.Deserialize<AdvertenciasDePlantillaDeTurnosCalculadas>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
