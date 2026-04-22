// HU-114: Tests de round-trip JSON para IntervaloClasificado.
// CA-7: sobrevive round-trip JSON con System.Text.Json preservando Intervalo y Concepto.
// IntervaloClasificado es un record con constructor primario publico - STJ lo maneja
// nativamente. IntervaloTemporal usa [JsonConstructor] en su ctor privado (PR 142).
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

public class IntervaloClasificadoSerializacionTests
{
    // ---------- CA-7: round-trip JSON preserva Intervalo y Concepto ----------

    [Fact]
    public void RoundTrip_PreservaConcepto_CuandoOrdinariaDiurna()
    {
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));
        var original = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json);

        restaurado.Should().NotBeNull();
        restaurado!.Concepto.Should().Be(Concepto.OrdinariaDiurna);
    }

    [Fact]
    public void RoundTrip_PreservaDuracionEnMinutos_CuandoOrdinariaDiurna()
    {
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));
        var original = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos.Should().Be(540);
    }

    [Fact]
    public void RoundTrip_PreservaConceptoYDuracion_CuandoIntervaloCruzaMedianoche()
    {
        // Intervalo nocturno: 22:00 -> 06:00+1 = 480 min
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(22, 0)),
            new MomentoDelDia(new TimeOnly(6, 0), 1));
        var original = new IntervaloClasificado(intervalo, Concepto.ExtraNocturna);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json);

        restaurado.Should().NotBeNull();
        restaurado!.Concepto.Should().Be(Concepto.ExtraNocturna);
        restaurado.DuracionEnMinutos.Should().Be(480);
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoIntervaloOrdinario()
    {
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));
        var original = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaConceptoDominicalFestiva_CuandoDescanso()
    {
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(10, 0)),
            new MomentoDelDia(new TimeOnly(10, 15)));
        var original = new IntervaloClasificado(intervalo, Concepto.Descanso);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloClasificado>(json);

        restaurado.Should().NotBeNull();
        restaurado!.Concepto.Should().Be(Concepto.Descanso);
        restaurado.DuracionEnMinutos.Should().Be(15);
    }
}
