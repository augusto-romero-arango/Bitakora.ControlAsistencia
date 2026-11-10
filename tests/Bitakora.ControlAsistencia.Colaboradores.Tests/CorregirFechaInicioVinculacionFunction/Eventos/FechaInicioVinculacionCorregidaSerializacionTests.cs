using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction.Eventos;

/// <summary>
/// Sin test "sin registro falla": el payload es plano (DateOnly) y no depende de ningun resolver
/// que proteger.
/// </summary>
public class FechaInicioVinculacionCorregidaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new FechaInicioVinculacionCorregida(new DateOnly(2026, 1, 10));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FechaInicioVinculacionCorregida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaInicio.Should().Be(new DateOnly(2026, 1, 10));
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConFechaDistanteEnElPasado()
    {
        var evento = new FechaInicioVinculacionCorregida(new DateOnly(2015, 3, 1));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FechaInicioVinculacionCorregida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaInicio.Should().Be(new DateOnly(2015, 3, 1));
    }
}
