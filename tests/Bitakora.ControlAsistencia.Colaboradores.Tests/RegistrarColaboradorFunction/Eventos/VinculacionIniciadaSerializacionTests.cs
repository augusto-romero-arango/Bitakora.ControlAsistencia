// Issue #330. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d). CA-5/CA-6.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction.Eventos;

/// <summary>
/// Verifica que VinculacionIniciada (payload plano: string + DateOnly, sin VOs anidados)
/// sobrevive un roundtrip de serializacion STJ con las opciones reales de Marten del dominio.
/// A diferencia de ColaboradorRegistrado, este evento NO necesita ConfigurarSerializacion propio
/// (ctor publico, tipos primitivos) -- mismo criterio que ProgramacionTurnoSolicitada -- asi que
/// no hay un test "sin registro falla": no hay ningun registro que proteger.
/// </summary>
public class VinculacionIniciadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-5: VinculacionIniciada persiste Codigo/FechaInicio tal como llegaron (sin default del
    // servidor) y sobrevive el roundtrip completo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new VinculacionIniciada("COL-001", new DateOnly(2026, 1, 15));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<VinculacionIniciada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Codigo.Should().Be("COL-001");
        deserializado.FechaInicio.Should().Be(new DateOnly(2026, 1, 15));
    }
}
