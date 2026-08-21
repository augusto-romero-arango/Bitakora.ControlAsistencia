// HorasDiscriminadas - payload plano (100% primitivo) que viaja en DiaDepurado por el bus interno
// del BC: serializa y deserializa con STJ POR DEFECTO, sin el resolver custom de Marten. Esa es la
// cura del payload lossy -- ningun consumidor depende de nuestra serializacion interna.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class HorasDiscriminadasTests
{
    private static HorasDiscriminadas CrearConDatos() => new(
        new Dictionary<string, int>
        {
            ["OrdinariaDiurna"] = 420,
            ["ExtraDiurna"] = 30,
            ["Retardo"] = 15
        },
        ["entro 06:15, retardo 15min"]);

    // CA-1: round-trip con el serializador POR DEFECTO (sin TypeInfoResolver custom). No lanza
    // NotSupportedException y preserva MinutosPorConcepto y Trazabilidad sin perdida.
    [Fact]
    public void RoundTrip_PreservaMinutosPorConceptoYTrazabilidad_ConSerializadorPorDefecto()
    {
        var original = CrearConDatos();
        var opciones = new JsonSerializerOptions();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosPorConcepto.Should().BeEquivalentTo(original.MinutosPorConcepto);
        restaurado.Trazabilidad.Should().BeEquivalentTo(original.Trazabilidad);
    }

    // CA-1 (barrera anti-regresion): con el resolver POR DEFECTO sin ningun ConfigurarSerializacion,
    // la deserializacion DEBE tener exito porque el payload es 100% primitivo. Si alguien reintroduce
    // un tipo de dominio rico (ctor/campos privados), fallaria con NotSupportedException.
    [Fact]
    public void Deserializar_TieneExito_ConResolverPorDefectoSinRegistros()
    {
        var json = JsonSerializer.Serialize(CrearConDatos());
        var opciones = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var act = () => JsonSerializer.Deserialize<HorasDiscriminadas>(json, opciones);

        act.Should().NotThrow();
        act()!.MinutosPorConcepto["OrdinariaDiurna"].Should().Be(420);
    }

    // CA-1: el diccionario vacio (dia sin horas calculables) round-trip a un diccionario vacio,
    // no a null. La trazabilidad vacia se preserva igual.
    [Fact]
    public void RoundTrip_PreservaColeccionesVacias_CuandoDiaSinHorasCalculables()
    {
        var original = new HorasDiscriminadas(new Dictionary<string, int>(), []);
        var opciones = new JsonSerializerOptions();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosPorConcepto.Should().BeEmpty();
        restaurado.Trazabilidad.Should().BeEmpty();
    }
}
