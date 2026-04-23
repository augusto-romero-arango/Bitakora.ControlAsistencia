// Issue #143: Tests de serializacion de IntervaloTemporal con las opciones reales de Marten.
// CA-5: round-trip usando ConfiguracionSerializacionControlHoras.CrearOpcionesMarten().
// CA-6: sin registro en el resolver, la deserializacion falla (barrera anti-regresion).
// Los tests del Issue #112 (IntervaloTemporalSerializacionTests.cs) usaban STJ vanilla
// con [JsonConstructor]. Al alinear con ADR-0015 ese atributo desaparece y el round-trip
// solo funciona con las opciones que registran ConfigurarSerializacion.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IntervaloTemporalSerializacionMartenTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

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
    public void RoundTrip_PreservaOffsetFinYDuracion_CuandoRangoCruzaMedianoche()
    {
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(22, 0)),
            new MomentoDelDia(new TimeOnly(6, 0), 1));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(original);
        restaurado!.Fin.DiaOffset.Should().Be(1);
        restaurado.DuracionEnMinutos.Should().Be(480);
    }

    // CA-5: round-trip verifica que Inicio y Fin se reconstruyen correctamente
    [Fact]
    public void RoundTrip_PreservaMomentosInicioyFin_CuandoIntervaloDiurno()
    {
        var inicio = new MomentoDelDia(new TimeOnly(14, 0));
        var fin = new MomentoDelDia(new TimeOnly(18, 30));
        var original = IntervaloTemporal.Crear(inicio, fin);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Inicio.Should().Be(inicio);
        restaurado.Fin.Should().Be(fin);
        restaurado.DuracionEnMinutos.Should().Be(270);
    }

    // CA-6: barrera anti-regresion.
    // Si alguien borra IntervaloTemporal.ConfigurarSerializacion(resolver) de ConfigurarResolver,
    // este test falla porque el round-trip ya no puede reconstruir el objeto.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeIntervaloTemporal()
    {
        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opciones = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));

        // Con resolver vacio, STJ puede serializar (lee propiedades publicas Inicio y Fin)
        // pero no puede deserializar (no hay constructor publico ni typeInfo.CreateObject).
        var json = JsonSerializer.Serialize(original, opciones);

        var act = () => JsonSerializer.Deserialize<IntervaloTemporal>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
