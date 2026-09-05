// Issue #603 CA-1: ExtraAgregado sobrevive un roundtrip de serializacion STJ -- requerido por
// Marten -- transportando FranjaOrdinaria (la franja contenedora RESULTANTE, ya con la hija) como
// payload rico.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarSubFranjaFunction.Eventos;

public class ExtraAgregadoSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000603");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaResultanteTraeElExtra()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConExtra(new TimeOnly(5, 0), new TimeOnly(6, 0));
        var evento = ExtraAgregado.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<ExtraAgregado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }

    // CA-regresion: si alguien olvida registrar ExtraAgregado en
    // ConfiguracionSerializacionProgramacion.ConfigurarResolver, este test falla.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeExtraAgregado()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))
            .ConExtra(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var evento = ExtraAgregado.Crear(TurnoId, franja);
        var json = JsonSerializer.Serialize(evento, CrearOpcionesMarten());

        var opcionesSinResolver = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<ExtraAgregado>(json, opcionesSinResolver);

        act.Should().Throw<NotSupportedException>();
    }
}
