// HU-105: Test de serializacion roundtrip para MarcacionRegistrada
// Requerido por regla 16: todo evento persistido en Marten debe sobrevivir Serialize -> Deserialize

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction.Eventos;

/// <summary>
/// Verifica que MarcacionRegistrada sobrevive un roundtrip de serializacion STJ.
/// Requerido porque Marten usa STJ con PropertyNamingPolicy=null (PascalCase)
/// y el evento tiene constructor privado y propiedades con private set.
/// Ver ADR-0013 y feedback: ConfigurarSerializacion es obligatorio.
/// </summary>
public class MarcacionRegistradaSerializacionTests
{
    // Replica las opciones que Marten usa: PropertyNamingPolicy = null (PascalCase)
    private static JsonSerializerOptions CrearOpcionesMarten()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        MarcacionRegistrada.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = null // Marten fuerza null
        };
    }

    // Verifica todos los campos incluyendo los opcionales con valores reales
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConDatosCompletos()
    {
        var timestamp = new DateTime(2026, 3, 15, 8, 9, 0);
        var evento = new MarcacionRegistrada("EMP-001", timestamp, "ENTRADA", "DEV-001");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EmpleadoId.Should().Be("EMP-001");
        deserializado.TimestampNormalizado.Should().Be(timestamp);
        deserializado.TipoMarcacion.Should().Be("ENTRADA");
        deserializado.DispositivoId.Should().Be("DEV-001");
    }

    // Verifica que los campos opcionales null se preservan correctamente en el roundtrip
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoCamposOpcionalesSonNulos()
    {
        var timestamp = new DateTime(2026, 3, 15, 8, 9, 0);
        var evento = new MarcacionRegistrada("EMP-002", timestamp, null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionRegistrada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EmpleadoId.Should().Be("EMP-002");
        deserializado.TimestampNormalizado.Should().Be(timestamp);
        deserializado.TipoMarcacion.Should().BeNull();
        deserializado.DispositivoId.Should().BeNull();
    }
}
