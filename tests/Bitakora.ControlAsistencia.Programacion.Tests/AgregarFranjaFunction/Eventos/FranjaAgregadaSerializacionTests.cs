// Issue #602 CA-1: FranjaAgregada sobrevive un roundtrip de serializacion STJ -- requerido por
// Marten -- transportando FranjaOrdinaria (VO con descansos y sede prearmada) como payload rico.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarFranjaFunction.Eventos;

public class FranjaAgregadaSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000602");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaTraeDescansoYSede()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0),
            // El descanso cae en el tramo posterior a medianoche de la ordinaria nocturna: su
            // offset +1 es explicito, como en FranjaOrdinariaTests (convencion de #598/#600).
            descansos: [SubFranja.Crear(new TimeOnly(2, 0), new TimeOnly(2, 30),
                diaOffsetInicio: 1, diaOffsetFin: 1)],
            sede: sede);
        var evento = FranjaAgregada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FranjaAgregada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaNoTraeSedeNiHijas()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = FranjaAgregada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FranjaAgregada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
    }
}
