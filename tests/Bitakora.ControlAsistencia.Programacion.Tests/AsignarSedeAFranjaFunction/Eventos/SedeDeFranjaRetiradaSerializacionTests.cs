// Issue #606 CA-1: SedeDeFranjaRetirada sobrevive un roundtrip de serializacion STJ -- requerido
// por Marten -- transportando FranjaOrdinaria (VO sin sede, o con sede) como payload rico.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarSedeAFranjaFunction.Eventos;

public class SedeDeFranjaRetiradaSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000606");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    // Caso principal: la franja resultante de un retiro nunca trae sede -- la clave "sede" se
    // omite del JSON (CA-4 de #335).
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaNoTraeSede()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));
        var evento = SedeDeFranjaRetirada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeDeFranjaRetirada>(json, opciones);

        json.Should().NotContain("\"sede\"");
        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaTraeSede()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0), sede: sede);
        var evento = SedeDeFranjaRetirada.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<SedeDeFranjaRetirada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }
}
