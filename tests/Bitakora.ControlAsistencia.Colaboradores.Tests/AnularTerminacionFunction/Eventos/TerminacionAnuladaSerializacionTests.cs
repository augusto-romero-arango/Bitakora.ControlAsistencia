// Issue #354. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction.Eventos;

/// <summary>
/// Verifica que TerminacionAnulada (sin payload -- el hecho es el evento mismo) sobrevive un
/// roundtrip de serializacion STJ con las opciones reales de Marten del dominio. El harness tolera
/// records sin propiedades (CommandHandlerTestBase ignora "No members were found for comparison."
/// en Then/And), pero el roundtrip de serializacion no pasa por ese camino: aqui se verifica
/// directamente que Serialize -> Deserialize reconstruye una instancia no nula, sin necesitar
/// ConfigurarSerializacion (no hay ningun campo que mapear).
/// </summary>
public class TerminacionAnuladaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-1: TerminacionAnulada sobrevive el roundtrip completo aunque no tenga ningun campo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_SinPayload()
    {
        var evento = new TerminacionAnulada();
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TerminacionAnulada>(json, opciones);

        deserializado.Should().NotBeNull();
    }
}
