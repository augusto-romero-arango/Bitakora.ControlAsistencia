using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.InstalarDispositivoFunction.Eventos;

// Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado inline.
public class DispositivoInstaladoSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConElDispositivoIdOpaco()
    {
        var evento = new DispositivoInstalado("DISP-100");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DispositivoInstalado>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
