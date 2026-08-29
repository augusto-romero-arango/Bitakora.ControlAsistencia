using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.EstamparSedeCuandoSedeDeMarcacionResuelta.Eventos;

public class SedeDeMarcacionIdentificadaSerializacionTests
{
    private static readonly string StreamId = "cd:EMP-001:20260315";
    private static readonly DateTime Timestamp = new(2026, 3, 15, 8, 9, 0);

    // Round-trip con las opciones reales de Marten: el evento solo sobrevive si
    // ConfiguracionSerializacionControlHoras.ConfigurarResolver lo registra (ctor privado).
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new SedeDeMarcacionIdentificada(
            StreamId, Timestamp, "DEV-001", "001", "Sede Principal", "CC-100");
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeDeMarcacionIdentificada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.TimestampNormalizado.Should().Be(Timestamp);
        deserializado.DispositivoId.Should().Be("DEV-001");
        deserializado.CodigoSede.Should().Be("001");
        deserializado.NombreSede.Should().Be("Sede Principal");
        deserializado.CentroDeCostos.Should().Be("CC-100");
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoCentroDeCostosEsNulo()
    {
        var evento = new SedeDeMarcacionIdentificada(
            StreamId, Timestamp, "DEV-001", "001", "Sede Principal", null);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeDeMarcacionIdentificada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.CodigoSede.Should().Be("001");
        deserializado.CentroDeCostos.Should().BeNull();
    }
}
