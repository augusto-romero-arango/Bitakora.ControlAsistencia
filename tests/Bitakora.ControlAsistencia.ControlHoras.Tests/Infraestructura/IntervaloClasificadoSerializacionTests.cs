// Issue #114: Tests de round-trip JSON para IntervaloClasificado con opciones reales de Marten.
// CA-7: round-trip usando ConfiguracionSerializacionControlHoras.CrearOpcionesMarten().
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IntervaloClasificadoSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    [Fact]
    public void RoundTrip_PreservaIntervaloYConcepto_CuandoOrdinariaDiurna()
    {
        var original = new IntervaloClasificado(
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)),
            Concepto.OrdinariaDiurna);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Intervalo.Should().Be(original.Intervalo);
        restaurado.Concepto.Should().Be(Concepto.OrdinariaDiurna);
    }

    [Fact]
    public void RoundTrip_PreservaConcepto_CuandoExtraNocturnaDominicalFestiva()
    {
        var original = new IntervaloClasificado(
            CrearIntervalo(new TimeOnly(22, 0), new TimeOnly(23, 30)),
            Concepto.ExtraNocturnaDominicalFestiva);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Concepto.Should().Be(Concepto.ExtraNocturnaDominicalFestiva);
    }

    [Fact]
    public void RoundTrip_PreservaDuracionEnMinutos_CuandoIntervaloDe90Minutos()
    {
        var original = new IntervaloClasificado(
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(9, 30)),
            Concepto.OrdinariaDiurna);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos.Should().Be(90);
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoMismosValores()
    {
        var original = new IntervaloClasificado(
            CrearIntervalo(new TimeOnly(14, 0), new TimeOnly(18, 0)),
            Concepto.OrdinariaDiurna);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json, opciones);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaIntervaloYDuracion_CuandoCruzaMedianoche()
    {
        var original = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(22, 0)),
                new MomentoDelDia(new TimeOnly(6, 0), 1)),
            Concepto.OrdinariaNocturna);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Intervalo.Should().Be(original.Intervalo);
        restaurado.DuracionEnMinutos.Should().Be(480);
    }
}
