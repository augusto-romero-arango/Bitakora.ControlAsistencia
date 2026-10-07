using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class JornadaDePlantillaSemanalSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000867");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 30), HorasYMinutos.Crear(9, 0),
            HorasYMinutos.Crear(4, 0), 2);

    [Fact]
    public void RoundTrip_ReconstruyeJornadaAsignada_ConLimitesYVersion()
    {
        var evento = JornadaDePlantillaSemanalAsignada.Crear(PlantillaId, JornadaId, Limites(), 7);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<JornadaDePlantillaSemanalAsignada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
        restaurado.JornadaId.Should().Be(JornadaId);
        restaurado.Limites.Should().Be(Limites());
        restaurado.VersionJornada.Should().Be(7);
    }

    [Fact]
    public void RoundTrip_ReconstruyeJornadaQuitada_ConDatosCompletos()
    {
        var evento = JornadaDePlantillaSemanalQuitada.Crear(PlantillaId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<JornadaDePlantillaSemanalQuitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeJornadaDePlantillaSemanalAsignada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            JornadaDePlantillaSemanalAsignada.Crear(PlantillaId, JornadaId, Limites(), 7), opciones);

        var act = () => JsonSerializer.Deserialize<JornadaDePlantillaSemanalAsignada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeJornadaDePlantillaSemanalQuitada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(JornadaDePlantillaSemanalQuitada.Crear(PlantillaId), opciones);

        var act = () => JsonSerializer.Deserialize<JornadaDePlantillaSemanalQuitada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
