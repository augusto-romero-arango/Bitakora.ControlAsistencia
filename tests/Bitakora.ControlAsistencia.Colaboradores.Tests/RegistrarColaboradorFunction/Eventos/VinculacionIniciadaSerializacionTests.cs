using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction.Eventos;

/// <summary>
/// Sin test "sin registro falla": el payload es plano (string + DateOnly) y no depende de ningun
/// resolver que proteger.
/// </summary>
public class VinculacionIniciadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

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

    // Evolucion aditiva (MEF-ADR-0005): un JSON persistido antes de existir CodigoSede deserializa con
    // sede null.
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
