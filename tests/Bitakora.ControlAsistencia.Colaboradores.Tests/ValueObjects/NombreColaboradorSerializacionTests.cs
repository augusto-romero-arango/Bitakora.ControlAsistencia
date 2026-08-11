// HU-348 CA-6: round-trip de serializacion (patron Marten) para NombreColaborador.
// ConfiguracionSerializacionColaboradores todavia no existe (mismo razonamiento que
// IdentificacionSerializacionTests.cs): el resolver se arma localmente con
// NombreColaborador.ConfigurarSerializacion.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class NombreColaboradorSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        NombreColaborador.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver, PropertyNamingPolicy = null };
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoLos4ComponentesEstanPresentes()
    {
        var original = NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<NombreColaborador>(json, opciones);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaAusenciaDeSegundos_CuandoSegundosSonNull()
    {
        var original = NombreColaborador.Crear("Luis", null, "Barreto", null);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<NombreColaborador>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.NombreCompleto.Should().Be("Luis Barreto");
    }

    // Barrera anti-regresion: sin el resolver custom, STJ no puede reconstruir NombreColaborador --
    // su unico constructor es privado y no hay [JsonConstructor] (proscrito por MEF-ADR-0012).
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeNombreColaborador()
    {
        var original = NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");
        var json = JsonSerializer.Serialize(original, CrearOpciones());
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var act = () => JsonSerializer.Deserialize<NombreColaborador>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
