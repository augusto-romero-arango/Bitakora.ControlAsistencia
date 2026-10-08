using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class AdvertenciasDePlantillaSemanalCalculadasSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000889");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConAdvertenciasCompletas()
    {
        var advertencias = new[]
        {
            AdvertenciaPlantillaSemanal.SuperaHorasSemanales(1, 120),
            AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
            AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Martes, 30),
            AdvertenciaPlantillaSemanal.DiaSinTurno(2, DiaSemana.Domingo)
        };
        var evento = AdvertenciasDePlantillaSemanalCalculadas.Crear(PlantillaId, advertencias);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<AdvertenciasDePlantillaSemanalCalculadas>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
        restaurado.Advertencias.Should().Equal(advertencias);
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConPlantillaSinJornada()
    {
        var evento = AdvertenciasDePlantillaSemanalCalculadas.Crear(
            PlantillaId, [AdvertenciaPlantillaSemanal.PlantillaSinJornada()]);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<AdvertenciasDePlantillaSemanalCalculadas>(json, opciones);

        restaurado!.Advertencias.Should().Equal(AdvertenciaPlantillaSemanal.PlantillaSinJornada());
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDelEvento()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            AdvertenciasDePlantillaSemanalCalculadas.Crear(PlantillaId, []), opciones);

        var act = () => JsonSerializer.Deserialize<AdvertenciasDePlantillaSemanalCalculadas>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
