using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction.Eventos;

// Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado inline.
public class NombreSedeModificadoSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConElNombreNuevo()
    {
        var evento = new NombreSedeModificado("Sede Renombrada");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<NombreSedeModificado>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
