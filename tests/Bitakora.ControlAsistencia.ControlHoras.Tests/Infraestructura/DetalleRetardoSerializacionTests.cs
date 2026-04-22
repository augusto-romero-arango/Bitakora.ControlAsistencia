// Issue #114: Tests de round-trip JSON para DetalleRetardo con las opciones reales de Marten.
// CA-9: round-trip usando ConfiguracionSerializacionControlHoras.CrearOpcionesMarten().
// CA-10: sin registro en el resolver, la deserializacion falla (barrera anti-regresion).
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class DetalleRetardoSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    [Fact]
    public void RoundTrip_PreservaMinutosRetardados_CuandoDetalleConUnRetardo()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))
        };
        var original = DetalleRetardo.Crear(retardados, compensados);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosRetardados.Should().Be(30);
    }

    [Fact]
    public void RoundTrip_PreservaMinutosCompensados_CuandoDetalleConUnaCompensacion()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))
        };
        var original = DetalleRetardo.Crear(retardados, compensados);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosCompensados.Should().Be(20);
    }

    [Fact]
    public void RoundTrip_PreservaRetardoNeto_CuandoCompensacionParcial()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))
        };
        var original = DetalleRetardo.Crear(retardados, compensados);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.RetardoNeto.Should().Be(10);
    }

    [Fact]
    public void RoundTrip_PreservaCantidadDeIntervalos_CuandoMultiplesIntervalos()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20)),
            CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 25))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))
        };
        var original = DetalleRetardo.Crear(retardados, compensados);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.TiempoRetardado.Should().HaveCount(2);
        restaurado.TiempoCompensado.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_PreservaContenidoDeIntervalos_VerificandoPrimerIntervalo()
    {
        var intervaloRetardo = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var retardados = new List<IntervaloTemporal> { intervaloRetardo };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))
        };
        var original = DetalleRetardo.Crear(retardados, compensados);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.TiempoRetardado[0].Should().Be(intervaloRetardo);
        restaurado.TiempoRetardado[0].DuracionEnMinutos.Should().Be(30);
    }

    // CA-10: barrera contra regresiones que borren la linea de registro en ConfigurarResolver.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroConfiguradoDeDetalleRetardo()
    {
        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opciones = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };
        var original = DetalleRetardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))]);
        var json = JsonSerializer.Serialize(original, opciones);

        var act = () => JsonSerializer.Deserialize<DetalleRetardo>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
