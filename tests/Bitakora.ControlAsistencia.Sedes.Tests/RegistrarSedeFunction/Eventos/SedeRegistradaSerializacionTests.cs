// Round-trip con las opciones REALES de Marten del dominio, nunca un resolver armado inline.
// SedeRegistrada es un record plano sin VOs con ctor privado: se reconstruye igual con o sin
// resolver custom, asi que no aplica el test "sin registro falla".

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RegistrarSedeFunction.Eventos;

public class SedeRegistradaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new SedeRegistrada("SEDE-001", "Sede Principal", "Bogota", "Calle 100 # 10-20");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<SedeRegistrada>(json, opciones);

        restaurado.Should().Be(evento);
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConCiudadYDireccionNulas()
    {
        var evento = new SedeRegistrada("SEDE-002", "Sede Secundaria", null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<SedeRegistrada>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
