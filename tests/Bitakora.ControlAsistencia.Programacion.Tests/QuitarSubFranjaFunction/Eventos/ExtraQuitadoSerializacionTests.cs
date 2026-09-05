// Issue #605 CA-1: ExtraQuitado sobrevive un roundtrip de serializacion STJ -- requerido por
// Marten -- transportando FranjaOrdinaria (la franja contenedora RESULTANTE, ya sin la hija) como
// payload rico.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarSubFranjaFunction.Eventos;

public class ExtraQuitadoSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000605");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoFranjaResultanteYaNoTraeElExtra()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
            .ConExtra(new TimeOnly(5, 0), new TimeOnly(6, 0));
        var evento = ExtraQuitado.Crear(TurnoId, franja);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<ExtraQuitado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Franja.Should().Be(franja);
        deserializado.Franja.ToString().Should().Be(franja.ToString());
    }

    // CA-regresion: si alguien olvida registrar ExtraQuitado en
    // ConfiguracionSerializacionProgramacion.ConfigurarResolver, este test falla.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeExtraQuitado()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));
        var evento = ExtraQuitado.Crear(TurnoId, franja);
        var json = JsonSerializer.Serialize(evento, CrearOpcionesMarten());

        var opcionesSinResolver = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<ExtraQuitado>(json, opcionesSinResolver);

        act.Should().Throw<NotSupportedException>();
    }
}
