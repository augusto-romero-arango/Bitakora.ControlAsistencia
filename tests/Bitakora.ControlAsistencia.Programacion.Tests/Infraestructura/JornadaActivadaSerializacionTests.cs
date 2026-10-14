using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class JornadaActivadaSerializacionTests
{
    private static readonly Guid JornadaId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a38");

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new JornadaActivada(JornadaId);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<JornadaActivada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.JornadaId.Should().Be(JornadaId);
    }
}
