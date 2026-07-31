// Issue #143: Tests de serializacion de IntervaloTemporal con las opciones reales de Marten.
// CA-5: round-trip usando ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten().
// CA-6: sin registro en el resolver, la deserializacion falla (barrera anti-regresion).
// Los tests del Issue #112 (IntervaloTemporalSerializacionTests.cs) usaban STJ vanilla
// con [JsonConstructor]. Al alinear con ADR-0015 ese atributo desaparece y el round-trip
// solo funciona con las opciones que registran ConfigurarSerializacion.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IntervaloTemporalSerializacionMartenTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten();

    // CA-5: round-trip diurno con opciones reales de Marten
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

    // CA-5: round-trip nocturno con offset
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

    // CA-5: round-trip verifica que el intervalo se reconstruye igual al original
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

    // CA-6: barrera anti-regresion.
    // Si alguien borra IntervaloTemporal.ConfigurarSerializacion(resolver) de ConfigurarResolver,
    // este test falla porque sin typeInfo.CreateObject registrado STJ no puede instanciar
    // un tipo con ctor privado.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeIntervaloTemporal()
    {
        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opciones = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));

        // Con resolver vacio, STJ no puede deserializar: el tipo no expone propiedades
        // publicas ni un ctor accesible que permita reconstruirlo.
        var json = JsonSerializer.Serialize(original, opciones);

        var act = () => JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
