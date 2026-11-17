using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class JornadaDePlantillaDeTurnosSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001284");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeJornadaAsignada_ConDatosCompletos()
    {
        var evento = JornadaDePlantillaDeTurnosAsignada.Crear(PlantillaId, JornadaId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<JornadaDePlantillaDeTurnosAsignada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
        restaurado.JornadaId.Should().Be(JornadaId);
    }

    // Los streams ya escritos guardan la copia de los limites y la version de la Jornada; STJ
    // ignora por defecto las propiedades que el tipo ya no declara.
    [Fact]
    public void Deserializar_ReconstruyeJornadaAsignada_CuandoElJsonPersistidoTraeLimitesYVersionJornada()
    {
        var opciones = CrearOpcionesMarten();
        var limites = JsonSerializer.Serialize(
            LimitesJornada.Crear(HorasYMinutos.Crear(42, 30), HorasYMinutos.Crear(9, 0), HorasYMinutos.Crear(4, 0), 2),
            opciones);
        var json = $$"""
            {"PlantillaId":"{{PlantillaId}}","JornadaId":"{{JornadaId}}","Limites":{{limites}},"VersionJornada":7}
            """;

        var restaurado = JsonSerializer.Deserialize<JornadaDePlantillaDeTurnosAsignada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
        restaurado.JornadaId.Should().Be(JornadaId);
    }

    [Fact]
    public void RoundTrip_ReconstruyeJornadaQuitada_ConDatosCompletos()
    {
        var evento = JornadaDePlantillaDeTurnosQuitada.Crear(PlantillaId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<JornadaDePlantillaDeTurnosQuitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeJornadaDePlantillaDeTurnosAsignada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            JornadaDePlantillaDeTurnosAsignada.Crear(PlantillaId, JornadaId), opciones);

        var act = () => JsonSerializer.Deserialize<JornadaDePlantillaDeTurnosAsignada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeJornadaDePlantillaDeTurnosQuitada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(JornadaDePlantillaDeTurnosQuitada.Crear(PlantillaId), opciones);

        var act = () => JsonSerializer.Deserialize<JornadaDePlantillaDeTurnosQuitada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
