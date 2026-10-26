using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class CierreDeLaVisperaActualizadoSerializacionTests
{
    private const string StreamId = "cd:EMP-001:20260421";
    private static readonly DateTime Cierre = new(2026, 4, 21, 6, 0, 0);

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConCierre()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = CierreDeLaVisperaActualizado.Crear(StreamId, Cierre);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<CierreDeLaVisperaActualizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Cierre.Should().Be(Cierre);
    }

    [Fact]
    public void RoundTrip_ConservaCierreNull_CuandoLaVisperaNoTrabaja()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = CierreDeLaVisperaActualizado.Crear(StreamId, null);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<CierreDeLaVisperaActualizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Cierre.Should().BeNull();
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeCierreDeLaVisperaActualizado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(CierreDeLaVisperaActualizado.Crear(StreamId, Cierre), opciones);

        var act = () => JsonSerializer.Deserialize<CierreDeLaVisperaActualizado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
