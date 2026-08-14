// HU-348 CA-6: round-trip de serializacion (patron Marten) para Identificacion.
// ConfiguracionSerializacionColaboradores todavia no existe -- este issue no introduce ningun
// evento persistido (ver "Investigacion del planner" del issue #348), asi que el resolver se
// arma localmente con Identificacion.ConfigurarSerializacion, igual que
// Programacion.Tests/ValueObjects/SubFranjaSerializacionTests.cs hace con SubFranja antes de que
// un evento persistido la reclame como payload.
// Issue #381 (CA-5): el numero persistido y rehidratado es SIEMPRE el YA LIMPIO -- la limpieza es
// invariante del VO (se aplica en Crear), asi que lo que llega a JSON nunca contiene el original
// con caracteres invalidos.
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

    // CA-5: el numero persistido y rehidratado es el YA LIMPIO -- Crear limpia ANTES de que el VO
    // exista, asi que el JSON nunca ve el original con espacios/guiones/puntos.
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

    // El contrato central de TipoIdentificacion es la FORMA de lo persistido, no solo que el
    // round-trip cierre: "lo persistido es SIEMPRE el codigo literal, jamas un numero" (issue #348,
    // razon por la que el tipo no es un enum C#). El round-trip de arriba pasaria en verde aunque
    // el tipo se guardara como sombra numerica o como objeto anidado -- solo esta asercion sobre el
    // JSON lo impide, y es lo que blinda los streams ya escritos el dia que alguien "simplifique"
    // el mapping.
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

    // Issue #371: unica consecuencia observable del refactor FUERA del borde HTTP. La rehidratacion
    // llama TipoIdentificacion.Desde con el valor crudo del payload, y Desde ahora normaliza: un
    // "tipo": "cc" corrupto en mt_events (que antes reventaba con ArgumentException) rehidrata a la
    // instancia canonica CC, preservando la identidad. El issue lo declara comportamiento aceptable
    // -- el write path nunca lo produce (solo persiste el codigo canonico, ver
    // Serializar_PersisteElTipoComoCodigoLiteral_...); este test fija que la tolerancia es
    // deliberada y no un descuido.
    [Fact]
    public void Deserializar_RehidrataAlTipoCanonico_CuandoElTipoPersistidoNoEstaNormalizado()
    {
        const string jsonCorrupto = """{"tipo":"cc","numero":"1098765432"}""";

        var restaurado = JsonSerializer.Deserialize<Identificacion>(jsonCorrupto, CrearOpciones());

        restaurado.Should().Be(Identificacion.Crear(TipoIdentificacion.CC, "1098765432"));
        restaurado!.ToString().Should().Be("CC-1098765432", "la clave de stream siempre es la canonica");
    }

    // Barrera anti-regresion: sin el resolver custom (equivalente a olvidar registrar el VO), STJ
    // no puede reconstruir Identificacion -- su unico constructor es privado y no hay
    // [JsonConstructor] (proscrito por MEF-ADR-0012).
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
