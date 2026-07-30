// HU-129: Crear estructuras agregadas DesgloseFranja y DesgloseHoras
// CA-1: MinutosPorConcepto agrupa intervalos por concepto y suma sus duraciones correctamente
// CA-2: MinutosPorConcepto omite los conceptos que no aparecen en Intervalos
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

/// <summary>
/// Tests de DesgloseFranja - estructura agregada del desglose de una sola franja.
/// Interfaz publica: constructor primario (Programada, Intervalos, Retardo), MinutosPorConcepto.
/// </summary>
public class DesgloseFranjaTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    private static DetalleFranjaOrdinaria CrearFranjaProgramada() =>
        new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], []);

    // ---------- CA-1: agrupa por concepto y suma duraciones ----------

    [Fact]
    public void MinutosPorConcepto_AgrupaPorConceptoYSuma_CuandoMismoConceptoEnVariosIntervalos()
    {
        // Dos intervalos de OrdinariaDiurna: 240 min + 240 min = 480 min
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)), Concepto.OrdinariaDiurna),
            new(CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)
        };
        var franja = new DesgloseFranja(CrearFranjaProgramada(), intervalos, Retardo.Vacio);

        var resultado = franja.MinutosPorConcepto;

        resultado.Should().ContainKey(Concepto.OrdinariaDiurna);
        resultado[Concepto.OrdinariaDiurna].Should().Be(480);
    }

    [Fact]
    public void MinutosPorConcepto_DistingueConceptosEnElDiccionario_CuandoIntervalosDeConceptosDiferentes()
    {
        // 120 min nocturno + 540 min diurno
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(8, 0)), Concepto.OrdinariaNocturna),
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)
        };
        var franja = new DesgloseFranja(CrearFranjaProgramada(), intervalos, Retardo.Vacio);

        var resultado = franja.MinutosPorConcepto;

        resultado[Concepto.OrdinariaNocturna].Should().Be(120);
        resultado[Concepto.OrdinariaDiurna].Should().Be(540);
    }

    [Fact]
    public void MinutosPorConcepto_SumaConceptosExtra_CuandoIntervaloDiurnoYExtraDiurnaPresentes()
    {
        // 480 min ordinaria diurna + 60 min extra diurna
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(16, 0)), Concepto.OrdinariaDiurna),
            new(CrearIntervalo(new TimeOnly(16, 0), new TimeOnly(17, 0)), Concepto.ExtraDiurna)
        };
        var franja = new DesgloseFranja(CrearFranjaProgramada(), intervalos, Retardo.Vacio);

        var resultado = franja.MinutosPorConcepto;

        resultado[Concepto.OrdinariaDiurna].Should().Be(480);
        resultado[Concepto.ExtraDiurna].Should().Be(60);
    }

    [Fact]
    public void MinutosPorConcepto_ContieneSoloLosConceptosPresentes_CuandoUnSoloIntervalo()
    {
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)
        };
        var franja = new DesgloseFranja(CrearFranjaProgramada(), intervalos, Retardo.Vacio);

        var resultado = franja.MinutosPorConcepto;

        resultado.Should().HaveCount(1);
        resultado[Concepto.OrdinariaDiurna].Should().Be(540);
    }

    // ---------- CA-2: omite conceptos ausentes ----------

    [Fact]
    public void MinutosPorConcepto_OmiteConceptos_CuandoNoApareceEnIntervalos()
    {
        // Solo OrdinariaDiurna en los intervalos; el resto de conceptos no deben figurar
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)
        };
        var franja = new DesgloseFranja(CrearFranjaProgramada(), intervalos, Retardo.Vacio);

        var resultado = franja.MinutosPorConcepto;

        resultado.Should().NotContainKey(Concepto.ExtraDiurna);
        resultado.Should().NotContainKey(Concepto.OrdinariaNocturna);
        resultado.Should().NotContainKey(Concepto.ExtraNocturna);
        resultado.Should().NotContainKey(Concepto.Descanso);
        resultado.Should().NotContainKey(Concepto.DominicalFestivaDiurna);
    }

    [Fact]
    public void MinutosPorConcepto_EstaVacio_CuandoSinIntervalos()
    {
        var franja = new DesgloseFranja(CrearFranjaProgramada(), [], Retardo.Vacio);

        franja.MinutosPorConcepto.Should().BeEmpty();
    }
}
