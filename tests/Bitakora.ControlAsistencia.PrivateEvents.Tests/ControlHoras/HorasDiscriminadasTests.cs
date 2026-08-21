// HorasDiscriminadas - payload plano (100% primitivo) que viaja en DiaDepurado por el bus interno
// del BC: serializa y deserializa con STJ POR DEFECTO, sin el resolver custom de Marten. Esa es la
// cura del payload lossy -- ningun consumidor depende de nuestra serializacion interna.
//
// Issue #424: HorasPorConcepto (ex MinutosPorConcepto) habla horas liquidables (decimal), no minutos.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class HorasDiscriminadasTests
{
    private static HorasDiscriminadas CrearConDatos() => new(
        new Dictionary<string, decimal>
        {
            ["OrdinariaDiurna"] = 7.00m,
            ["ExtraDiurna"] = 0.50m,
            ["Retardo"] = 0.25m
        },
        ["entro 06:15, retardo 15min"]);

    // CA-1/CA-2: round-trip con el serializador POR DEFECTO (sin TypeInfoResolver custom). No lanza
    // NotSupportedException y preserva HorasPorConcepto (decimal) y Trazabilidad sin perdida.
    [Fact]
    public void RoundTrip_PreservaHorasPorConceptoYTrazabilidad_ConSerializadorPorDefecto()
    {
        var original = CrearConDatos();
        var opciones = new JsonSerializerOptions();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.HorasPorConcepto.Should().BeEquivalentTo(original.HorasPorConcepto);
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
        act()!.HorasPorConcepto["OrdinariaDiurna"].Should().Be(7.00m);
    }

    // CA-2: el diccionario vacio (dia sin horas calculables) round-trip a un diccionario vacio,
    // no a null. La trazabilidad vacia se preserva igual.
    [Fact]
    public void RoundTrip_PreservaColeccionesVacias_CuandoDiaSinHorasCalculables()
    {
        var original = new HorasDiscriminadas(new Dictionary<string, decimal>(), []);
        var opciones = new JsonSerializerOptions();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.HorasPorConcepto.Should().BeEmpty();
        restaurado.Trazabilidad.Should().BeEmpty();
    }
}
