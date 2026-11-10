using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction.Eventos;

/// <summary>
/// Verifica que MarcacionRegistrada sobrevive un roundtrip de serializacion STJ dentro del event
/// store (opciones reales de Marten, con el resolver custom registrado), y que NO sobrevive fuera
/// de ese canal -- ni con las mismas opciones sin el registro, ni con las del canal de bus.
/// </summary>
public class MarcacionRegistradaSerializacionTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateTime TimestampConSegundos = new(2026, 3, 15, 8, 9, 59);
    private static readonly DateTime TimestampNormalizadoEsperado = new(2026, 3, 15, 8, 9, 0);

    // Opciones REALES de Marten del dominio, no un resolver inline: asi los RoundTrip_* son la barrera
    // contra borrar el registro en ConfiguracionSerializacionControlHoras.ConfigurarResolver (sin el, el
    // ctor vacio privado es inalcanzable para STJ y fallan con NotSupportedException).
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoDatosCompletos()
    {
        var evento = MarcacionRegistrada.Crear(CodigoColaborador, TimestampConSegundos, "ENTRADA", "DEV-001");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CodigoColaborador.Should().Be(CodigoColaborador);
        deserializado.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
        deserializado.TipoMarcacion.Should().Be("ENTRADA");
        deserializado.DispositivoId.Should().Be("DEV-001");
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoCamposOpcionalesSonNulos()
    {
        var evento = MarcacionRegistrada.Crear("EMP-002", TimestampNormalizadoEsperado, null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CodigoColaborador.Should().Be("EMP-002");
        deserializado.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
        deserializado.TipoMarcacion.Should().BeNull();
        deserializado.DispositivoId.Should().BeNull();
    }

    // Fija el comportamiento del canal sin resolver: con un resolver vacio STJ no puede invocar el ctor
    // vacio privado. El borrado del registro lo detectan los RoundTrip_* de arriba.
    [Fact]
    public void Deserializar_LanzaNotSupportedException_CuandoElResolverNoRegistraElTipo()
    {
        var evento = MarcacionRegistrada.Crear(
            CodigoColaborador, TimestampNormalizadoEsperado, "ENTRADA", "DEV-001");
        var opcionesSinRegistro = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = null
        };
        var json = JsonSerializer.Serialize(evento, opcionesSinRegistro);

        var act = () => JsonSerializer.Deserialize<MarcacionRegistrada>(json, opcionesSinRegistro);

        act.Should().Throw<NotSupportedException>();
    }

    // MarcacionRegistrada no cruza el bus: con el serializador del canal (sin resolver custom) lanza
    // NotSupportedException. Es la barrera inversa a la de HorasDiscriminadas (CA-ADR-0025 seccion 4):
    // alli el payload plano DEBE sobrevivir sin resolver; aqui el evento rico solo vive en el event store.
    [Fact]
    public void Deserializar_LanzaNotSupportedException_CuandoUsaElSerializadorDelCanalDeBus()
    {
        var evento = MarcacionRegistrada.Crear(
            CodigoColaborador, TimestampNormalizadoEsperado, "ENTRADA", "DEV-001");
        var opcionesDelBus = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(evento, opcionesDelBus);

        var act = () => JsonSerializer.Deserialize<MarcacionRegistrada>(json, opcionesDelBus);

        act.Should().Throw<NotSupportedException>();
    }
}
