// Issue #114: Tests de round-trip JSON para Retardo con las opciones reales de Marten.
// CA-9: round-trip usando ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten().
// CA-10: sin registro en el resolver, la deserializacion falla (barrera anti-regresion).
// Retardo expone solo RetardoNeto y ToString() publicamente; los campos privados
// se verifican a traves del contrato IEquatable y la representacion ToString().
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class RetardoSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten();

    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoCompensacionParcial()
    {
        var original = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))]);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Retardo>(json, opciones);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaRetardoNeto_CuandoCompensacionParcial()
    {
        var original = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))]);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Retardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.RetardoNeto.Should().Be(10);
    }

    [Fact]
    public void RoundTrip_PreservaToString_CuandoMultiplesIntervalos()
    {
        var original = Retardo.Crear(
            [
                CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20)),
                CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 25))
            ],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Retardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_PreservaRetardoNetoCero_CuandoCompensacionExcedeRetardo()
    {
        // Escenario del review: 20 min retardados, 30 min compensados.
        // RetardoNeto debe viajar como 0 (saturado) y los minutos crudos preservarse.
        var original = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Retardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.RetardoNeto.Should().Be(0);
        restaurado.Should().Be(original);
        restaurado.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_PreservaVacio()
    {
        var original = Retardo.Vacio;
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Retardo>(json, opciones);

        restaurado.Should().Be(original);
        restaurado!.RetardoNeto.Should().Be(0);
    }

    // CA-10: barrera contra regresiones que borren la linea de registro en ConfigurarResolver.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroConfiguradoDeRetardo()
    {
        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opciones = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };
        var original = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))]);
        var json = JsonSerializer.Serialize(original, opciones);

        var act = () => JsonSerializer.Deserialize<Retardo>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
