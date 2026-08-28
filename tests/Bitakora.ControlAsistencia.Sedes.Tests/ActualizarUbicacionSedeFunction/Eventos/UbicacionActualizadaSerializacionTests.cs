using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActualizarUbicacionSedeFunction.Eventos;

// Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado inline.
public class UbicacionActualizadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new UbicacionActualizada("Medellin", "Carrera 50 # 20-30");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<UbicacionActualizada>(json, opciones);

        restaurado.Should().Be(evento);
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConCiudadYDireccionNulas()
    {
        var evento = new UbicacionActualizada(null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<UbicacionActualizada>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
