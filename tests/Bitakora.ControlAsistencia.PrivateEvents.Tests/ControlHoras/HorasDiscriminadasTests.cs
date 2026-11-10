// HorasDiscriminadas es un payload plano (100% primitivo) que viaja en DiaDepurado por el bus: serializa
// con STJ POR DEFECTO, sin el resolver custom de Marten, para que ningun consumidor dependa de nuestra
// serializacion interna. HorasPorConcepto son horas liquidables (decimal), no minutos.

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

    // Barrera anti-regresion: con el resolver POR DEFECTO sin ningun ConfigurarSerializacion,
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

    // El diccionario vacio (dia sin horas calculables) vuelve como diccionario vacio, no como null.
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
