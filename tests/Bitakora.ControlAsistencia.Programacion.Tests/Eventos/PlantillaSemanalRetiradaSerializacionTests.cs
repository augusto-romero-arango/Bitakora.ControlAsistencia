// Issue #623: round-trip de serializacion de PlantillaSemanalRetirada (seccion 6d del harness)

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

// Round-trip con las opciones REALES de Marten del dominio: un resolver armado inline haria pasar
// el test aunque PlantillaSemanalRetirada no este registrado en ConfiguracionSerializacionProgramacion.
public class PlantillaSemanalRetiradaSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000623");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = PlantillaSemanalRetirada.Crear(PlantillaId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<PlantillaSemanalRetirada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.PlantillaId.Should().Be(PlantillaId);
    }

    // CA-regresion: si nadie registra PlantillaSemanalRetirada.ConfigurarSerializacion en
    // ConfiguracionSerializacionProgramacion.ConfigurarResolver, el ctor privado impide a STJ
    // reconstruirlo -- este test hace visible el olvido.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDePlantillaSemanalRetirada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(PlantillaSemanalRetirada.Crear(PlantillaId), opciones);

        var act = () => JsonSerializer.Deserialize<PlantillaSemanalRetirada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
