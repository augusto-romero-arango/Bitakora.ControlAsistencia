using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IntervaloTemporalSerializacionMartenTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_PreservaIgualdadYDuracion_CuandoRangoDiurno()
    {
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(original);
        restaurado!.DuracionEnMinutos.Should().Be(540);
        restaurado.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_PreservaIntervaloYDuracion_CuandoRangoCruzaMedianoche()
    {
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(22, 0)),
            new MomentoDelDia(new TimeOnly(6, 0), 1));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(original);
        restaurado!.DuracionEnMinutos.Should().Be(480);
    }

    [Fact]
    public void RoundTrip_PreservaIgualdadYDuracion_CuandoIntervaloDiurnoConMinutosImpares()
    {
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(14, 0)),
            new MomentoDelDia(new TimeOnly(18, 30)));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(original);
        restaurado!.DuracionEnMinutos.Should().Be(270);
    }

    // Si alguien borra IntervaloTemporal.ConfigurarSerializacion(resolver) de ConfigurarResolver, este
    // test falla: sin typeInfo.CreateObject STJ no puede instanciar un tipo con ctor privado.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeIntervaloTemporal()
    {
        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opciones = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));

        var json = JsonSerializer.Serialize(original, opciones);

        var act = () => JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
