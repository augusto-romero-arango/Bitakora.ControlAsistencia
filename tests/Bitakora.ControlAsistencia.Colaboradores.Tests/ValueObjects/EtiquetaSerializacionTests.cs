using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class EtiquetaSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        Etiqueta.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver, PropertyNamingPolicy = null };
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoEtiquetaValida()
    {
        var original = Etiqueta.Crear("Área", "Tecnología");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Etiqueta>(json, opciones);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaFormasOriginalesYNormalizadas_CuandoEtiquetaTieneTildesYMayusculas()
    {
        var original = Etiqueta.Crear("Área", "Tecnología");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Etiqueta>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Categoria.Should().Be("Área");
        restaurado.Valor.Should().Be("Tecnología");
        restaurado.CategoriaNormalizada.Should().Be("area");
        restaurado.ValorNormalizado.Should().Be("tecnologia");
    }

    // El JSON crudo debe llevar las 4 formas: el round-trip pasaria aunque se persistiera solo una forma
    // y se recalculara la otra al leer.
    [Fact]
    public void Serializar_PersisteLasCuatroFormasPorCampo_CuandoEtiquetaValida()
    {
        var original = Etiqueta.Crear("Área", "Tecnología");

        var json = JsonSerializer.Serialize(original, CrearOpciones());

        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("categoria").GetString().Should().Be("Área");
        documento.RootElement.GetProperty("categoriaNormalizada").GetString().Should().Be("area");
        documento.RootElement.GetProperty("valor").GetString().Should().Be("Tecnología");
        documento.RootElement.GetProperty("valorNormalizado").GetString().Should().Be("tecnologia");
    }

    // Sin el resolver custom STJ no reconstruye Etiqueta: su unico constructor es privado y
    // [JsonConstructor] esta proscrito (MEF-ADR-0012).
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeEtiqueta()
    {
        var original = Etiqueta.Crear("Área", "Tecnología");
        var json = JsonSerializer.Serialize(original, CrearOpciones());
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var act = () => JsonSerializer.Deserialize<Etiqueta>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
