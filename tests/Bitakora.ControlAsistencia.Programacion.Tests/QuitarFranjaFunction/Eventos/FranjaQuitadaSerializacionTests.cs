// Issue #604 CA-1: FranjaQuitada sobrevive un roundtrip de serializacion STJ -- requerido por
// Marten -- transportando la FranjaOrdinaria completa que se quito (con descanso y sede).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarFranjaFunction.Eventos;

public class FranjaQuitadaSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000604");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaTraeDescansoYSede()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(15, 0), new TimeOnly(19, 0),
            descansos: [SubFranja.Crear(new TimeOnly(17, 0), new TimeOnly(17, 30))],
            sede: sede);
        var evento = FranjaQuitada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FranjaQuitada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaNoTraeSedeNiHijas()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = FranjaQuitada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FranjaQuitada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
    }
}
