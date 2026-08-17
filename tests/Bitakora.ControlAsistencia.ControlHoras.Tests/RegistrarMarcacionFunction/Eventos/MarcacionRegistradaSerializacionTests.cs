// HU-105 / Issue #275: Proteger MarcacionRegistrada con factory Crear() y ctores privados.
// Requerido por regla 16: todo evento persistido en Marten debe sobrevivir Serialize -> Deserialize.
// CA-6: el round-trip con las opciones de Marten y el resolver custom sigue verde, ahora como
// UNICA via de reconstruccion -- ya no hay ctor publico que STJ pueda usar como fallback.

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

    // Usa las opciones REALES de Marten del dominio (regla 6d) -- no un resolver armado inline que
    // solo registre este tipo. Esa eleccion es lo que convierte a los dos RoundTrip_* de abajo en la
    // barrera contra el borrado de la linea de registro en
    // ConfiguracionSerializacionControlHoras.ConfigurarResolver: sin ella, el ctor vacio privado es
    // inalcanzable para STJ y ambos fallan con NotSupportedException.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    // Verifica todos los campos incluyendo los opcionales con valores reales.
    // Issue #275: el evento se construye con el factory Crear -- unica via posible con ctor privado.
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

    // Verifica que los campos opcionales null se preservan correctamente en el roundtrip
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

    // Documenta POR QUE el registro en ConfigurarResolver es obligatorio: con las mismas opciones de
    // Marten pero un resolver vacio, STJ no tiene forma de invocar el ctor vacio privado. Quien
    // detecta el borrado del registro son los RoundTrip_* de arriba (usan las opciones reales del
    // dominio); este test fija el comportamiento del canal sin resolver.
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

    // CA-5: guardrail anti-regresion -- MarcacionRegistrada ya no cruza el bus (issue #270) y,
    // cerrado el ctor (issue #275), tampoco es reconstruible con el serializador del canal (sin
    // resolver custom): la operacion lanza NotSupportedException. Es la barrera que CA-ADR-0025
    // describe para HorasDiscriminadas, en sentido inverso -- alli el payload plano DEBE sobrevivir
    // sin resolver; aqui el evento de dominio rico NO debe hacerlo, porque solo vive en el event
    // store (CA-ADR-0025 seccion 4).
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
