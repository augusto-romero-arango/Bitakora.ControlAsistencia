using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction.Eventos;

/// <summary>
/// Sin test "sin registro falla": el payload es plano (DateOnly) y no depende de ningun resolver
/// que proteger.
/// </summary>
public class VinculacionTerminadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new VinculacionTerminada(new DateOnly(2026, 6, 1));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<VinculacionTerminada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaEfectiva.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConFechaEfectivaFutura()
    {
        var evento = new VinculacionTerminada(new DateOnly(2030, 1, 1));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<VinculacionTerminada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaEfectiva.Should().Be(new DateOnly(2030, 1, 1));
    }
}
