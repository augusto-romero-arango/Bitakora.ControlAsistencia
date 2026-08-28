using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarCentroDeCostosFunction.Eventos;

// Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado inline.
public class CentroDeCostosRetiradoSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_SinPayload()
    {
        var evento = new CentroDeCostosRetirado();
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<CentroDeCostosRetirado>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
