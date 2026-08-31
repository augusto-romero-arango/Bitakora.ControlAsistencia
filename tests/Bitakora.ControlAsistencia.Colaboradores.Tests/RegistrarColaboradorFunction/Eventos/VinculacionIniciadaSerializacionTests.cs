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
        deserializado.CodigoSede.Should().BeNull();
    }

    // Issue #520 (CA-1/CA-3): VinculacionIniciada con CodigoSede presente sobrevive el roundtrip
    // completo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConCodigoSedePresente()
    {
        var evento = new VinculacionIniciada("COL-001", new DateOnly(2026, 1, 15), "BOG");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<VinculacionIniciada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Codigo.Should().Be("COL-001");
        deserializado.FechaInicio.Should().Be(new DateOnly(2026, 1, 15));
        deserializado.CodigoSede.Should().Be("BOG");
    }

    // CA-5: evolucion ADITIVA (MEF-ADR-0005) -- un JSON persistido ANTES de este issue, sin el campo
    // CodigoSede, deserializa con sede null. No es mover/renombrar un tipo (MEF-ADR-0036 no aplica):
    // el mismo alias sigue resolviendo al mismo tipo, solo con un campo nuevo opcional.
    [Fact]
    public void Deserializar_AsumeCodigoSedeNull_CuandoElJsonHistoricoNoTraeElCampo()
    {
        var jsonHistorico = """{"Codigo":"COL-001","FechaInicio":"2026-01-15"}""";
        var opciones = CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<VinculacionIniciada>(jsonHistorico, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Codigo.Should().Be("COL-001");
        deserializado.FechaInicio.Should().Be(new DateOnly(2026, 1, 15));
        deserializado.CodigoSede.Should().BeNull();
    }
}
