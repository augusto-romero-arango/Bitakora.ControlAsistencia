using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarDispositivoFunction.Eventos;

// Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado inline.
public class DispositivoRetiradoSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConElDispositivoIdOpaco()
    {
        var evento = new DispositivoRetirado("DISP-100");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DispositivoRetirado>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
