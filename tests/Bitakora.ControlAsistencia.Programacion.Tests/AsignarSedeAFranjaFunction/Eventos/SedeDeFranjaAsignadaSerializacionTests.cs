// Issue #606 CA-1: SedeDeFranjaAsignada sobrevive un roundtrip de serializacion STJ -- requerido
// por Marten -- transportando FranjaOrdinaria (VO con sede prearmada) como payload rico.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarSedeAFranjaFunction.Eventos;

public class SedeDeFranjaAsignadaSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000606");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaTraeSede()
    {
        var sede = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0), sede: sede);
        var evento = SedeDeFranjaAsignada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeDeFranjaAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }

    // Retrocompatibilidad con el formato de FranjaOrdinaria (CA-4 de #335): la clave "sede" se
    // omite del JSON cuando es null.
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaNoTraeSede()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));
        var evento = SedeDeFranjaAsignada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeDeFranjaAsignada>(json, opciones);

        json.Should().NotContain("\"sede\"");
        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
    }
}
