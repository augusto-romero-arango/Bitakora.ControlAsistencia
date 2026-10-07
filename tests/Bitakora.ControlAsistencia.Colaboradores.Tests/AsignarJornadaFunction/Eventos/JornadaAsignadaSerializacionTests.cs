using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarJornadaFunction.Eventos;

public class JornadaAsignadaSerializacionTests
{
    [Fact]
    public void RoundTrip_ReconstruyeJornadaAsignada_ConOpcionesMarten()
    {
        var jornadaId = Guid.Parse("12345678-1234-4123-8123-123456789abc");
        var opciones = ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

        var restaurado = JsonSerializer.Deserialize<JornadaAsignada>(
            JsonSerializer.Serialize(new JornadaAsignada(jornadaId), opciones), opciones);

        restaurado.Should().Be(new JornadaAsignada(jornadaId));
    }
}
