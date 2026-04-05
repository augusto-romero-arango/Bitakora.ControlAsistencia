// HU-2: Modelar value objects de franja temporal para turnos
// Cubre: CA-4, CA-6, CA-7, CA-8, CA-10, CA-11, CA-12, CA-13, CA-14, CA-15, CA-16,
//        CA-17, CA-18, CA-19, CA-20

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaOrdinariaTests
{
    // -------------------------------------------------------------------------
    // CA-8: factory static Crear con constructor privado
    // CA-7: HoraInicio == HoraFin rechazado
    // CA-4: DiaOffsetFin presente
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCrearse_CuandoHorasValidasSinOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        franja.HoraInicio.Should().Be(new TimeOnly(6, 0));
        franja.HoraFin.Should().Be(new TimeOnly(12, 0));
        franja.DiaOffsetFin.Should().Be(0);
    }

    [Fact]
    public void DebeCrearse_CuandoHorasValidasConOffsetUno()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.HoraInicio.Should().Be(new TimeOnly(22, 0));
        franja.HoraFin.Should().Be(new TimeOnly(6, 0));
        franja.DiaOffsetFin.Should().Be(1);
    }

    [Fact]
    public void DebeLanzarExcepcion_CuandoHoraInicioIgualHoraFin()
    {
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(8, 0));

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void DebeLanzarExcepcion_CuandoHoraInicioIgualHoraFinConOffset()
    {
        // Offset 0, fin < inicio pero no es franja valida si se da offset=0 con fin<inicio
        // y encima hora inicio == hora fin seria invalido
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(8, 0), diaOffsetFin: 1);

        act.Should().ThrowExactly<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // CA-10: duracion sin offset - semantica inicio-inclusivo, fin-exclusivo (CA-6)
    // CA-11: duracion con offset de dia
    // CA-12: conversion a horas decimales
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCalcularDuracionEnMinutos_CuandoFranjaSinOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(11, 0));

        franja.DuracionEnMinutos().Should().Be(180);
    }

    [Fact]
    public void DebeCalcularDuracionEnMinutos_CuandoFranjaConOffsetDia()
    {
        // 22:00 a 06:00+1 = 8 horas = 480 minutos
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(480);
    }

    [Fact]
    public void DebeConvertirAHorasDecimales_CuandoFranjaDeTresHoras()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(11, 0));

        franja.DuracionEnHorasDecimales().Should().Be(3.0);
    }

    [Fact]
    public void DebeConvertirAHorasDecimales_CuandoFranjaConOffsetDia()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.DuracionEnHorasDecimales().Should().Be(8.0);
    }

    // -------------------------------------------------------------------------
    // CA-13: inferencia de offset +1 cuando fin < inicio
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeInferirOffsetUnoDia_CuandoFinMenorQueInicio()
    {
        var offset = FranjaBase.InferirDiaOffset(new TimeOnly(22, 0), new TimeOnly(6, 0));

        offset.Should().Be(1);
    }

    [Fact]
    public void DebeInferirOffsetCero_CuandoFinMayorQueInicio()
    {
        var offset = FranjaBase.InferirDiaOffset(new TimeOnly(8, 0), new TimeOnly(16, 0));

        offset.Should().Be(0);
    }

    [Fact]
    public void DebeCrearseFranjaConOffset_CuandoSeUsaMetodoCrearConInferencia()
    {
        // El factory infiere diaOffsetFin=1 porque 06:00 < 22:00
        var franja = FranjaOrdinaria.CrearInfiriendoOffset(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.DiaOffsetFin.Should().Be(1);
        franja.DuracionEnMinutos().Should().Be(480);
    }

    // -------------------------------------------------------------------------
    // CA-20: ToString con formato legible
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeProducirFormatoLegible_CuandoFranjaSinOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        franja.ToString().Should().Be("(06:00-12:00)");
    }

    [Fact]
    public void DebeProducirFormatoLegible_CuandoFranjaConOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.ToString().Should().Be("(22:00-06:00+1)");
    }

    // -------------------------------------------------------------------------
    // CA-14: contencion temporal - franja hija dentro del rango de la ordinaria
    // CA-15: contencion con cruce de medianoche
    // CA-16: franja que excede el rango es rechazada
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeContener_CuandoDescansoEstaEnRangoOrdinariaDiurna()
    {
        var ordinaria = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        ordinaria.Contiene(descanso).Should().BeTrue();
    }

    [Fact]
    public void DebeContener_CuandoExtraEstaEnRangoOrdinariaDiurna()
    {
        var ordinaria = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        ordinaria.Contiene(extra).Should().BeTrue();
    }

    [Fact]
    public void DebeContener_CuandoDescansoEstaEnOrdinariaNocturnaConCruceMedianoche()
    {
        // Ordinaria: 22:00+0 a 06:00+1. Descanso: 23:00+0 a 23:15+0 - esta dentro
        var ordinaria = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);
        var descanso = FranjaDescanso.Crear(
            new TimeOnly(23, 0), new TimeOnly(23, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);

        ordinaria.Contiene(descanso).Should().BeTrue();
    }

    [Fact]
    public void DebeContener_CuandoDescansoEstaDespuesDeMedianoche()
    {
        // Ordinaria: 22:00+0 a 06:00+1. Descanso: 01:00+1 a 01:30+1 - esta dentro
        var ordinaria = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);
        var descanso = FranjaDescanso.Crear(
            new TimeOnly(1, 0), new TimeOnly(1, 30),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        ordinaria.Contiene(descanso).Should().BeTrue();
    }

    [Fact]
    public void NoDebeContener_CuandoExtraExcedeFinOrdinariaNocturna()
    {
        // Ordinaria: 22:00+0 a 06:00+1. Extra: 05:00+1 a 07:00+1 - excede el fin
        var ordinaria = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);
        var extra = FranjaExtra.Crear(
            new TimeOnly(5, 0), new TimeOnly(7, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        ordinaria.Contiene(extra).Should().BeFalse();
    }

    [Fact]
    public void NoDebeContener_CuandoDescansoEmpiezaAntesQueOrdinaria()
    {
        // Ordinaria: 08:00 a 12:00. Descanso: 07:30 a 08:30 - empieza antes
        var ordinaria = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var descanso = FranjaDescanso.Crear(new TimeOnly(7, 30), new TimeOnly(8, 30));

        ordinaria.Contiene(descanso).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // CA-17: deteccion de solapamiento entre franjas hijas
    // CA-18: franjas contiguas NO son solapadas (fin exclusivo - CA-6)
    // CA-19: solapamiento con offsets
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeDetectarSolapamiento_CuandoDescansoYExtraSeIntersectan()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));
        var extra = FranjaExtra.Crear(new TimeOnly(10, 15), new TimeOnly(11, 0));

        FranjaOrdinaria.SeSolapan(descanso, extra).Should().BeTrue();
    }

    [Fact]
    public void DebeDetectarSolapamiento_CuandoDosDescansosSeIntersectan()
    {
        var descanso1 = FranjaDescanso.Crear(new TimeOnly(9, 0), new TimeOnly(9, 30));
        var descanso2 = FranjaDescanso.Crear(new TimeOnly(9, 15), new TimeOnly(9, 45));

        FranjaOrdinaria.SeSolapan(descanso1, descanso2).Should().BeTrue();
    }

    [Fact]
    public void NoDebeDetectarSolapamiento_CuandoFranjasContiguas()
    {
        // Fin exclusivo: 10:30 es el inicio del siguiente, no hay solapamiento
        var descanso1 = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));
        var descanso2 = FranjaDescanso.Crear(new TimeOnly(10, 30), new TimeOnly(11, 0));

        FranjaOrdinaria.SeSolapan(descanso1, descanso2).Should().BeFalse();
    }

    [Fact]
    public void NoDebeDetectarSolapamiento_CuandoFranjasDisjuntas()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));
        var extra = FranjaExtra.Crear(new TimeOnly(11, 0), new TimeOnly(12, 0));

        FranjaOrdinaria.SeSolapan(descanso, extra).Should().BeFalse();
    }

    [Fact]
    public void DebeDetectarSolapamiento_CuandoFranjasConOffsetSeIntersectan()
    {
        // Ambas en turno nocturno, se solapan en 22:30-23:00
        var extra1 = FranjaExtra.Crear(
            new TimeOnly(22, 0), new TimeOnly(23, 0),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var extra2 = FranjaExtra.Crear(
            new TimeOnly(22, 30), new TimeOnly(23, 30),
            diaOffsetInicio: 0, diaOffsetFin: 0);

        FranjaOrdinaria.SeSolapan(extra1, extra2).Should().BeTrue();
    }

    [Fact]
    public void NoDebeDetectarSolapamiento_CuandoFranjasConOffsetSonContiguas()
    {
        // extra1 termina donde extra2 empieza - fin exclusivo, no solapan
        var extra1 = FranjaExtra.Crear(
            new TimeOnly(22, 0), new TimeOnly(0, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);
        var extra2 = FranjaExtra.Crear(
            new TimeOnly(0, 0), new TimeOnly(2, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        FranjaOrdinaria.SeSolapan(extra1, extra2).Should().BeFalse();
    }
}
