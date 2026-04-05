// HU-2: Modelar value objects de franja temporal para turnos
// Cubre: CA-2, CA-5, CA-7, CA-8, CA-10, CA-12, CA-13, CA-21

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaDescansoTests
{
    // -------------------------------------------------------------------------
    // CA-8: factory static Crear con constructor privado
    // CA-7: HoraInicio == HoraFin rechazado
    // CA-5: FranjaDescanso tiene DiaOffsetInicio y DiaOffsetFin
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCrearse_CuandoHorasValidasSinOffset()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));

        descanso.HoraInicio.Should().Be(new TimeOnly(10, 0));
        descanso.HoraFin.Should().Be(new TimeOnly(10, 30));
        descanso.DiaOffsetInicio.Should().Be(0);
        descanso.DiaOffsetFin.Should().Be(0);
    }

    [Fact]
    public void DebeCrearse_CuandoDescansoEstaDespuesDeMedianoche()
    {
        // Descanso en turno nocturno: empieza y termina en el dia siguiente
        var descanso = FranjaDescanso.Crear(
            new TimeOnly(1, 0), new TimeOnly(1, 30),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        descanso.DiaOffsetInicio.Should().Be(1);
        descanso.DiaOffsetFin.Should().Be(1);
    }

    [Fact]
    public void DebeLanzarExcepcion_CuandoHoraInicioIgualHoraFinSinOffset()
    {
        var act = () => FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 0));

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void DebeLanzarExcepcion_CuandoHoraInicioIgualHoraFinConMismoOffset()
    {
        var act = () => FranjaDescanso.Crear(
            new TimeOnly(1, 0), new TimeOnly(1, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        act.Should().ThrowExactly<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // CA-10: duracion sin offset
    // CA-12: conversion a horas decimales
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCalcularDuracionEnMinutos_CuandoDescansoSinOffset()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        descanso.DuracionEnMinutos().Should().Be(15);
    }

    [Fact]
    public void DebeCalcularDuracionEnMinutos_CuandoDescansoConOffsets()
    {
        // Descanso 23:45+0 a 00:15+1 = 30 minutos cruzando medianoche
        var descanso = FranjaDescanso.Crear(
            new TimeOnly(23, 45), new TimeOnly(0, 15),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        descanso.DuracionEnMinutos().Should().Be(30);
    }

    [Fact]
    public void DebeConvertirAHorasDecimales_CuandoDescansoDeMediaHora()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));

        descanso.DuracionEnHorasDecimales().Should().Be(0.5);
    }

    // -------------------------------------------------------------------------
    // CA-13: inferencia de offset en descansos que cruzan medianoche
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeCrearseConOffsets_CuandoSeUsaMetodoCrearConInferencia()
    {
        // fin < inicio => offset fin = 1 relativo al offset inicio
        var descanso = FranjaDescanso.CrearInfiriendoOffset(
            new TimeOnly(23, 45), new TimeOnly(0, 15),
            diaOffsetInicio: 0);

        descanso.DiaOffsetFin.Should().Be(1);
        descanso.DuracionEnMinutos().Should().Be(30);
    }

    // -------------------------------------------------------------------------
    // CA-21: subclases heredan ToString() de la base
    // CA-20: formato legible
    // -------------------------------------------------------------------------

    [Fact]
    public void DebeHeredarToStringDeLaBase_CuandoDescansoSinOffset()
    {
        // FranjaDescanso no define su propio ToString, usa el de FranjaBase (CA-21)
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));

        descanso.ToString().Should().Be("(10:00-10:30)");
    }

    [Fact]
    public void DebeHeredarToStringDeLaBase_CuandoDescansoConOffsetFin()
    {
        var descanso = FranjaDescanso.Crear(
            new TimeOnly(23, 45), new TimeOnly(0, 15),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        descanso.ToString().Should().Be("(23:45-00:15+1)");
    }
}
