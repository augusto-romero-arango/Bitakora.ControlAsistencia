// HU-106: Test de serializacion roundtrip para MarcacionAdicionada
// Requerido por regla 16: todo evento persistido en Marten debe sobrevivir Serialize -> Deserialize

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.Eventos;

/// <summary>
/// Verifica que MarcacionAdicionada sobrevive un roundtrip de serializacion STJ.
/// Requerido porque Marten usa STJ con PropertyNamingPolicy=null (PascalCase)
/// y el evento tiene constructor privado y propiedades con private set.
/// Ver ADR-0013 y patron canonico en TurnoCreadoSerializacionTests (Programacion).
/// </summary>
public class MarcacionAdicionadaSerializacionTests
{
    private static readonly string StreamId = "EMP-001:2026-03-15";
    private static readonly string EmpleadoId = "EMP-001";
    private static readonly DateTime Timestamp = new DateTime(2026, 3, 15, 8, 15, 0);

    // Regla 16: usa CrearOpcionesMarten() que registra ConfigurarSerializacion de todos los tipos
    // MarcacionAdicionada se registra en ConfiguracionSerializacionControlHoras.ConfigurarResolver
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new MarcacionAdicionada(StreamId, EmpleadoId, Timestamp, "ENTRADA", "DEV-001");
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionAdicionada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.EmpleadoId.Should().Be(EmpleadoId);
        deserializado.TimestampNormalizado.Should().Be(Timestamp);
        deserializado.TipoMarcacion.Should().Be("ENTRADA");
        deserializado.DispositivoId.Should().Be("DEV-001");
    }

    // Verifica que los campos opcionales null se preservan correctamente en el roundtrip
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoCamposOpcionalesSonNulos()
    {
        var evento = new MarcacionAdicionada(StreamId, EmpleadoId, Timestamp, null, null);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionAdicionada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.EmpleadoId.Should().Be(EmpleadoId);
        deserializado.TimestampNormalizado.Should().Be(Timestamp);
        deserializado.TipoMarcacion.Should().BeNull();
        deserializado.DispositivoId.Should().BeNull();
    }
}
