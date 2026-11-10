using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class IdentificacionSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        Identificacion.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver, PropertyNamingPolicy = null };
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoIdentificacionValida()
    {
        var original = Identificacion.Crear(TipoIdentificacion.CC, "1098765432");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Identificacion>(json, opciones);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaTipoYNumero_CuandoIdentificacionEsPasaporte()
    {
        var original = Identificacion.Crear(TipoIdentificacion.PA, "AB1234567");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Identificacion>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Tipo.Should().BeSameAs(TipoIdentificacion.PA);
        restaurado.Numero.Should().Be("AB1234567");
    }

    [Fact]
    public void RoundTrip_PersisteYRehidrataElNumeroYaLimpio_CuandoElOriginalTraiaCaracteresInvalidos()
    {
        var original = Identificacion.Crear(TipoIdentificacion.CC, " ab-12.3 ");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Identificacion>(json, opciones);

        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("numero").GetString().Should().Be("AB123",
            "el numero persistido debe ser el ya limpio, nunca el original con caracteres invalidos");

        restaurado.Should().NotBeNull();
        restaurado!.Numero.Should().Be("AB123");
        restaurado.ToString().Should().Be("CC-AB123");
    }

    // Lo persistido es siempre el codigo literal, jamas un numero (por eso el tipo no es un enum C#): el
    // round-trip pasaria aunque se guardara como numero u objeto anidado, y esta asercion protege los
    // streams ya escritos.
    [Fact]
    public void Serializar_PersisteElTipoComoCodigoLiteral_CuandoIdentificacionValida()
    {
        var original = Identificacion.Crear(TipoIdentificacion.CC, "1098765432");

        var json = JsonSerializer.Serialize(original, CrearOpciones());

        using var documento = JsonDocument.Parse(json);
        var tipo = documento.RootElement.GetProperty("tipo");
        tipo.ValueKind.Should().Be(JsonValueKind.String, "el tipo nunca se persiste como numero");
        tipo.GetString().Should().Be("CC");
        documento.RootElement.GetProperty("numero").GetString().Should().Be("1098765432");
    }

    // Tolerancia deliberada: la rehidratacion pasa por TipoIdentificacion.Desde, que normaliza, asi que
    // un "tipo": "cc" en mt_events rehidrata a CC. El write path nunca lo produce.
    [Fact]
    public void Deserializar_RehidrataAlTipoCanonico_CuandoElTipoPersistidoNoEstaNormalizado()
    {
        const string jsonCorrupto = """{"tipo":"cc","numero":"1098765432"}""";

        var restaurado = JsonSerializer.Deserialize<Identificacion>(jsonCorrupto, CrearOpciones());

        restaurado.Should().Be(Identificacion.Crear(TipoIdentificacion.CC, "1098765432"));
        restaurado!.ToString().Should().Be("CC-1098765432", "la clave de stream siempre es la canonica");
    }

    // Sin el resolver custom STJ no reconstruye Identificacion: su unico constructor es privado y
    // [JsonConstructor] esta proscrito (MEF-ADR-0012).
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeIdentificacion()
    {
        var original = Identificacion.Crear(TipoIdentificacion.CC, "1098765432");
        var json = JsonSerializer.Serialize(original, CrearOpciones());
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var act = () => JsonSerializer.Deserialize<Identificacion>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
