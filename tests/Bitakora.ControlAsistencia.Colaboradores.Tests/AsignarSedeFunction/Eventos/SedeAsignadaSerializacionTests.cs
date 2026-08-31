using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction.Eventos;

/// <summary>
/// Roundtrip con las opciones reales de Marten del dominio. Sin test "sin registro falla": el
/// payload es plano (string) y no depende de ningun resolver que proteger.
/// </summary>
public class SedeAsignadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new SedeAsignada("BOG");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CodigoSede.Should().Be("BOG");
    }

    // El evento nunca normaliza el codigo: la idempotencia del aggregate compara case-sensitive.
    [Fact]
    public void RoundTrip_PreservaElCaseOriginalDelCodigo()
    {
        var evento = new SedeAsignada("bog");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CodigoSede.Should().Be("bog");
    }
}
