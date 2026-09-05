using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction.Eventos;

public class PlantillaSemanalCreadaSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000620");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", 2);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<PlantillaSemanalCreada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.Nombre.Should().Be("Semana Cocina");
        deserializado.Semanas.Should().Be(2);
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoSemanasEsElMaximoPermitido()
    {
        var evento = PlantillaSemanalCreada.Crear(
            PlantillaId, "Plantilla Maxima", PlantillaSemanalCreada.MaximoSemanas);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<PlantillaSemanalCreada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Semanas.Should().Be(PlantillaSemanalCreada.MaximoSemanas);
    }

    // Guarda del registro en ConfigurarResolver: sin el, STJ no encuentra constructor publico ni
    // parameterless. Si este test dejara de lanzar, el resolver ya no seria necesario -- no lo es.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDePlantillaSemanalCreada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", 2), opciones);

        var act = () => JsonSerializer.Deserialize<PlantillaSemanalCreada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
