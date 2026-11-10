using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.Eventos;

public class MarcacionAdicionadaSerializacionTests
{
    private static readonly string StreamId = "cd:EMP-001:20260315";
    private static readonly string CodigoColaborador = "EMP-001";
    private static readonly DateTime Timestamp = new DateTime(2026, 3, 15, 8, 15, 0);

    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new MarcacionAdicionada(StreamId, CodigoColaborador, Timestamp, "ENTRADA", "DEV-001");
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionAdicionada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.CodigoColaborador.Should().Be(CodigoColaborador);
        deserializado.TimestampNormalizado.Should().Be(Timestamp);
        deserializado.TipoMarcacion.Should().Be("ENTRADA");
        deserializado.DispositivoId.Should().Be("DEV-001");
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoCamposOpcionalesSonNulos()
    {
        var evento = new MarcacionAdicionada(StreamId, CodigoColaborador, Timestamp, null, null);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<MarcacionAdicionada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.CodigoColaborador.Should().Be(CodigoColaborador);
        deserializado.TimestampNormalizado.Should().Be(Timestamp);
        deserializado.TipoMarcacion.Should().BeNull();
        deserializado.DispositivoId.Should().BeNull();
    }
}
