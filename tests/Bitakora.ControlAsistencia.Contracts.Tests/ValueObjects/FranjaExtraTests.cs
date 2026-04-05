// HU-2: Modelar value objects de franja temporal para turnos
// Cubre: CA-2, CA-5, CA-7, CA-8, CA-10, CA-12, CA-13, CA-21

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaExtraTests
{
    // -------------------------------------------------------------------------
    // CA-8: factory static Crear con constructor privado
    // CA-7: HoraInicio == HoraFin rechazado
    // CA-5: FranjaExtra tiene DiaOffsetInicio y DiaOffsetFin
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCrearse_CuandoHorasValidasSinOffset()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.HoraInicio.Should().Be(new TimeOnly(6, 0));
        extra.HoraFin.Should().Be(new TimeOnly(8, 0));
        extra.DiaOffsetInicio.Should().Be(0);
        extra.DiaOffsetFin.Should().Be(0);
    }

    [Fact]
    public void DebeCrearse_CuandoExtraEstaDespuesDeMedianoche()
    {
        var extra = FranjaExtra.Crear(
            new TimeOnly(2, 0), new TimeOnly(4, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        extra.DiaOffsetInicio.Should().Be(1);
        extra.DiaOffsetFin.Should().Be(1);
    }

    [Fact]
    public void DebeLanzarExcepcion_CuandoHoraInicioIgualHoraFinSinOffset()
    {
        var act = () => FranjaExtra.Crear(new TimeOnly(9, 0), new TimeOnly(9, 0));

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void DebeLanzarExcepcion_CuandoHoraInicioIgualHoraFinConMismoOffset()
    {
        var act = () => FranjaExtra.Crear(
            new TimeOnly(2, 0), new TimeOnly(2, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        act.Should().ThrowExactly<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // CA-10: duracion sin offset
    // CA-12: conversion a horas decimales
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCalcularDuracionEnMinutos_CuandoExtraSinOffset()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.DuracionEnMinutos().Should().Be(120);
    }

    [Fact]
    public void DebeCalcularDuracionEnMinutos_CuandoExtraConOffsets()
    {
        // Extra 23:00+0 a 01:00+1 = 2 horas = 120 minutos cruzando medianoche
        var extra = FranjaExtra.Crear(
            new TimeOnly(23, 0), new TimeOnly(1, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        extra.DuracionEnMinutos().Should().Be(120);
    }

    [Fact]
    public void DebeConvertirAHorasDecimales_CuandoExtraDeDosHoras()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.DuracionEnHorasDecimales().Should().Be(2.0);
    }

    // -------------------------------------------------------------------------
    // CA-13: inferencia de offset en extras que cruzan medianoche
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCrearseConOffsets_CuandoSeUsaMetodoCrearConInferencia()
    {
        // fin < inicio => offset fin = 1 relativo al offset inicio
        var extra = FranjaExtra.CrearInfiriendoOffset(
            new TimeOnly(22, 0), new TimeOnly(2, 0),
            diaOffsetInicio: 0);

        extra.DiaOffsetFin.Should().Be(1);
        extra.DuracionEnMinutos().Should().Be(240);
    }

    // -------------------------------------------------------------------------
    // CA-21: subclases heredan ToString() de la base
    // CA-20: formato legible
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeHeredarToStringDeLaBase_CuandoExtraSinOffset()
    {
        // FranjaExtra no define su propio ToString, usa el de FranjaBase (CA-21)
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.ToString().Should().Be("(06:00-08:00)");
    }

    [Fact]
    public void DebeHeredarToStringDeLaBase_CuandoExtraConOffsets()
    {
        var extra = FranjaExtra.Crear(
            new TimeOnly(22, 0), new TimeOnly(2, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        extra.ToString().Should().Be("(22:00-02:00+1)");
    }
}
