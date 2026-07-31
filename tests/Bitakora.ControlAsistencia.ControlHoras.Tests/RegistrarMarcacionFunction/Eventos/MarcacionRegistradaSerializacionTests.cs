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
    // Usa las opciones REALES de Marten del dominio (regla 6d) -- no un resolver armado inline que
    // solo registre este tipo. Si alguien borra la linea de registro en
    // ConfiguracionSerializacionControlHoras.ConfigurarResolver, el test
    // "Deserializar_Falla_CuandoResolverNoTieneRegistroDeMarcacionRegistrada" de abajo lo detecta.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    // Verifica todos los campos incluyendo los opcionales con valores reales.
    // Issue #275: el evento se construye con el factory Crear -- unica via posible con ctor privado.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var timestampCrudo = new DateTime(2026, 3, 15, 8, 9, 59);
        var evento = MarcacionRegistrada.Crear("EMP-001", timestampCrudo, "ENTRADA", "DEV-001");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EmpleadoId.Should().Be("EMP-001");
        deserializado.TimestampNormalizado.Should().Be(new DateTime(2026, 3, 15, 8, 9, 0));
        deserializado.TipoMarcacion.Should().Be("ENTRADA");
        deserializado.DispositivoId.Should().Be("DEV-001");
    }

    // Verifica que los campos opcionales null se preservan correctamente en el roundtrip
    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoCamposOpcionalesSonNulos()
    {
        var timestampCrudo = new DateTime(2026, 3, 15, 8, 9, 0);
        var evento = MarcacionRegistrada.Crear("EMP-002", timestampCrudo, null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EmpleadoId.Should().Be("EMP-002");
        deserializado.TimestampNormalizado.Should().Be(timestampCrudo);
        deserializado.TipoMarcacion.Should().BeNull();
        deserializado.DispositivoId.Should().BeNull();
    }

    // CA-regresion (regla 16): si alguien borra el registro de MarcacionRegistrada en
    // ConfiguracionSerializacionControlHoras.ConfigurarResolver, este test lo detecta -- sin el
    // registro, STJ no tiene forma de invocar el ctor vacio privado.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeMarcacionRegistrada()
    {
        var evento = MarcacionRegistrada.Crear(
            "EMP-001", new DateTime(2026, 3, 15, 8, 9, 0), "ENTRADA", "DEV-001");
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
    public void Deserializar_LanzaNotSupportedException_ConSerializadorDelCanalDeBus()
    {
        var evento = MarcacionRegistrada.Crear(
            "EMP-001", new DateTime(2026, 3, 15, 8, 9, 0), "ENTRADA", "DEV-001");
        var opcionesDelBus = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(evento, opcionesDelBus);

        var act = () => JsonSerializer.Deserialize<MarcacionRegistrada>(json, opcionesDelBus);

        act.Should().Throw<NotSupportedException>();
    }
}
