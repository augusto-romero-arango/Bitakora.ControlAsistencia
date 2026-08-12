// Issue #355. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d). CA-3.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction.Eventos;

/// <summary>
/// Verifica que EtiquetaRetirada (payload plano: string, sin VOs anidados) sobrevive un roundtrip
/// de serializacion STJ con las opciones reales de Marten del dominio. Este evento NO necesita
/// ConfigurarSerializacion propio (ctor publico, tipo primitivo) -- mismo criterio que
/// VinculacionTerminada/VinculacionIniciada -- asi que no hay un test "sin registro falla": no hay
/// ningun registro que proteger.
/// </summary>
public class EtiquetaRetiradaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-3: EtiquetaRetirada persiste la categoria normalizada tal como llego y sobrevive el
    // roundtrip completo.
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
