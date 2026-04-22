// HU-114: Tests de round-trip JSON para DetalleRetardo.
// CA-8: sobrevive round-trip JSON preservando TiempoRetardado, TiempoCompensado,
//       MinutosRetardados, MinutosCompensados y RetardoNeto.
//       Valida que [JsonConstructor] en el ctor privado funciona (patron PR 142).
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

public class DetalleRetardoSerializacionTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // ---------- CA-8: round-trip JSON con datos reales (no listas vacias) ----------

    [Fact]
    public void RoundTrip_PreservaMinutosRetardados_CuandoDetalleConUnRetardo()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))  // 30 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))  // 20 min
        };
        var original = DetalleRetardo.Crear(retardados, compensados);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosRetardados.Should().Be(30);
    }

    [Fact]
    public void RoundTrip_PreservaMinutosCompensados_CuandoDetalleConUnaCompensacion()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))  // 30 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))  // 20 min
        };
        var original = DetalleRetardo.Crear(retardados, compensados);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosCompensados.Should().Be(20);
    }

    [Fact]
    public void RoundTrip_PreservaRetardoNeto_CuandoCompensacionParcial()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))  // 30 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))  // 20 min => neto = 10
        };
        var original = DetalleRetardo.Crear(retardados, compensados);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json);

        restaurado.Should().NotBeNull();
        restaurado!.RetardoNeto.Should().Be(10);
    }

    [Fact]
    public void RoundTrip_PreservaCantidadDeIntervalosRetardados_CuandoMultiplesIntervalos()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20)),   // 20 min
            CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 25))    // 25 min => total 45 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))  // 30 min
        };
        var original = DetalleRetardo.Crear(retardados, compensados);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json);

        restaurado.Should().NotBeNull();
        restaurado!.TiempoRetardado.Should().HaveCount(2);
        restaurado.TiempoCompensado.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_PreservaContenidoDeIntervalosRetardados_VerificandoPrimerIntervalo()
    {
        var intervaloRetardo = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var retardados = new List<IntervaloTemporal> { intervaloRetardo };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))
        };
        var original = DetalleRetardo.Crear(retardados, compensados);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DetalleRetardo>(json);

        restaurado.Should().NotBeNull();
        restaurado!.TiempoRetardado[0].Should().Be(intervaloRetardo);
        restaurado.TiempoRetardado[0].DuracionEnMinutos.Should().Be(30);
    }
}
