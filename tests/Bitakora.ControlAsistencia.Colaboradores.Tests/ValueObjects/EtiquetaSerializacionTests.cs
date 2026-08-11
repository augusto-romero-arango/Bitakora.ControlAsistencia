// HU-353 CA-6: round-trip de serializacion (patron Marten) para Etiqueta.
// ConfiguracionSerializacionColaboradores todavia no existe -- este dominio nace sin evento
// persistido propio (ver IdentidadEventosColaboradores.TiposPersistidos, vacia hasta #355+), asi
// que el resolver se arma localmente con Etiqueta.ConfigurarSerializacion, igual que
// IdentificacionSerializacionTests.cs / NombreColaboradorSerializacionTests.cs hacen con sus VOs
// antes de que un evento persistido los reclame como payload.
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

    // Contrato central de la "doble forma persistida" (decision del planner, issue #353): el
    // evento debe cargar las 4 formas por campo, no solo la original ni solo la normalizada. Este
    // test sobre el JSON crudo blinda esa decision -- el round-trip de arriba pasaria en verde
    // aunque se persistiera solo una forma y se recalculara la otra al leer.
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

    // Barrera anti-regresion: sin el resolver custom (equivalente a olvidar registrar el VO), STJ
    // no puede reconstruir Etiqueta -- su unico constructor es privado y no hay [JsonConstructor]
    // (proscrito por MEF-ADR-0012).
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
