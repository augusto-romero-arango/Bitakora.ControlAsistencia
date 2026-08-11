// Issue #349. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction.Eventos;

/// <summary>
/// Verifica que VinculacionTerminada (payload plano: DateOnly, sin VOs anidados) sobrevive un
/// roundtrip de serializacion STJ con las opciones reales de Marten del dominio. Igual que
/// VinculacionIniciada, este evento NO necesita ConfigurarSerializacion propio (ctor publico, tipo
/// primitivo) -- no hay un test "sin registro falla": no hay ningun registro que proteger.
/// </summary>
public class VinculacionTerminadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-1: VinculacionTerminada persiste FechaEfectiva tal como llego (sin default del servidor)
    // y sobrevive el roundtrip completo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new VinculacionTerminada(new DateOnly(2026, 6, 1));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<VinculacionTerminada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaEfectiva.Should().Be(new DateOnly(2026, 6, 1));
    }

    // CA-2: una FechaEfectiva futura (preaviso) sobrevive el roundtrip igual que una pasada.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConFechaEfectivaFutura()
    {
        var evento = new VinculacionTerminada(new DateOnly(2030, 1, 1));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<VinculacionTerminada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaEfectiva.Should().Be(new DateOnly(2030, 1, 1));
    }
}
