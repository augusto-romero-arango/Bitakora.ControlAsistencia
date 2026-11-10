using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction.Eventos;

public class TerminacionAnuladaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_SinPayload()
    {
        var evento = new TerminacionAnulada();
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TerminacionAnulada>(json, opciones);

        deserializado.Should().NotBeNull();
    }
}
