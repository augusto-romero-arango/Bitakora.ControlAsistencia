// Issue #459. Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado
// inline (regla 16 / seccion 6d).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.DesactivarSedeFunction.Eventos;

public class SedeDesactivadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_SinPayload()
    {
        var evento = new SedeDesactivada();
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<SedeDesactivada>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
