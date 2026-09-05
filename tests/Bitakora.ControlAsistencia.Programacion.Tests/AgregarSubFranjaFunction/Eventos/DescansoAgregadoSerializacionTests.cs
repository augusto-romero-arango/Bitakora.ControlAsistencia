// Issue #603 CA-1: DescansoAgregado sobrevive un roundtrip de serializacion STJ -- requerido por
// Marten -- transportando FranjaOrdinaria (la franja contenedora RESULTANTE, ya con la hija) como
// payload rico.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarSubFranjaFunction.Eventos;

public class DescansoAgregadoSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000603");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaResultanteTraeElDescanso()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));
        var evento = DescansoAgregado.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DescansoAgregado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }

    // CA-regresion: si alguien olvida registrar DescansoAgregado en
    // ConfiguracionSerializacionProgramacion.ConfigurarResolver, este test falla.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeDescansoAgregado()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))
            .ConDescanso(new TimeOnly(9, 0), new TimeOnly(9, 15));
        var evento = DescansoAgregado.Crear(TurnoId, franja);
        var json = JsonSerializer.Serialize(evento, CrearOpcionesMarten());

        var opcionesSinResolver = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<DescansoAgregado>(json, opcionesSinResolver);

        act.Should().Throw<NotSupportedException>();
    }
}
