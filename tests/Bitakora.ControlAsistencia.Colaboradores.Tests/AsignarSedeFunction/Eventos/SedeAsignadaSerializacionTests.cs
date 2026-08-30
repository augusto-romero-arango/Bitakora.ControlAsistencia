// Issue #465. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction.Eventos;

/// <summary>
/// Verifica que SedeAsignada (payload plano: string, sin VOs anidados -- solo la referencia por
/// codigo al maestro de Sedes, CA-ADR-0029 islas) sobrevive un roundtrip de serializacion STJ con
/// las opciones reales de Marten del dominio. No necesita ConfigurarSerializacion propio (ctor
/// publico, tipo primitivo) -- no hay un test "sin registro falla": no hay ningun registro que
/// proteger (mismo criterio que VinculacionTerminadaSerializacionTests).
/// </summary>
public class SedeAsignadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-1: SedeAsignada persiste CodigoSede tal como llego y sobrevive el roundtrip completo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new SedeAsignada("BOG");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CodigoSede.Should().Be("BOG");
    }

    // CA-3: el codigo se persiste preservando el case original (comparacion exacta case-sensitive,
    // precedente #387) -- un lower-case no se normaliza en el evento.
    [Fact]
    public void RoundTrip_PreservaElCaseOriginalDelCodigo()
    {
        var evento = new SedeAsignada("bog");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.CodigoSede.Should().Be("bog");
    }
}
