using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction.Eventos;

/// <summary>
/// Sin test "sin registro falla": el payload es plano (string) y no depende de ningun resolver que
/// proteger.
/// </summary>
public class EtiquetaRetiradaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new EtiquetaRetirada("area");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<EtiquetaRetirada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CategoriaNormalizada.Should().Be("area");
    }
}
