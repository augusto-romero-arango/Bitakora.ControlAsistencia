using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction.Eventos;

public class PlantillaDeTurnosCreadaSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001280");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo 8 dias", 8);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<PlantillaDeTurnosCreada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.Nombre.Should().Be("Ciclo 8 dias");
        deserializado.DiasDelCiclo.Should().Be(8);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDePlantillaDeTurnosCreada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo 8 dias", 8), opciones);

        var act = () => JsonSerializer.Deserialize<PlantillaDeTurnosCreada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
