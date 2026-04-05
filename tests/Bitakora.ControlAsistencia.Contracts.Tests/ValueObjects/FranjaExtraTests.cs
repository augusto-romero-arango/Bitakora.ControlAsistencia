// Issue #2: Modelar value objects de franja temporal para turnos
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

/// <summary>
/// Tests de FranjaExtra - horas extras programadas dentro de una franja ordinaria.
/// Interfaz publica: Crear(...), DuracionEnMinutos(), DuracionEnHorasDecimales(), ToString().
/// </summary>
public class FranjaExtraTests
{
    // ---------- CA-2, CA-8: hereda de la abstraccion base, factory estatico ----------

    [Fact]
    public void Crear_RetornaFranjaExtra_CuandoDatosValidos()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.ToString().Should().Be("(06:00-08:00)");
    }

    [Fact]
    public void Crear_EsSubtipoDeAbstraccionBase_CuandoSeInstancia()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.Should().BeAssignableTo<FranjaTemporal>();
    }

    // ---------- CA-7: inicio == fin es rechazado ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoInicioYFinSonIguales()
    {
        var act = () => FranjaExtra.Crear(new TimeOnly(8, 0), new TimeOnly(8, 0));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.InicioYFinIguales}*");
    }

    // ---------- CA-5: tiene DiaOffsetInicio y DiaOffsetFin ----------

    [Fact]
    public void Crear_RetornaFranjaConOffset_CuandoDiaOffsetInicioYFinSonEspecificados()
    {
        // Extra en madrugada del dia siguiente: 04:00+1 -> 06:00+1
        var extra = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        extra.ToString().Should().Be("(04:00+1-06:00+1)");
    }

    // ---------- CA-10: duracion sin offset ----------

    [Fact]
    public void DuracionEnMinutos_Retorna120_CuandoExtraDeDosHoras()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.DuracionEnMinutos().Should().Be(120);
    }

    // ---------- CA-11: duracion con offset ----------

    [Fact]
    public void DuracionEnMinutos_Retorna120_CuandoExtraEnMadrugadaConOffset()
    {
        // Extra en la madrugada: 04:00+1 -> 06:00+1
        var extra = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        extra.DuracionEnMinutos().Should().Be(120);
    }

    // ---------- CA-12: conversores de duracion ----------

    [Fact]
    public void DuracionEnHorasDecimales_Retorna2_CuandoExtraDe120Minutos()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        extra.DuracionEnHorasDecimales().Should().Be(2.0);
    }

    // ---------- CA-20, CA-21: ToString() ----------

    [Fact]
    public void ToString_MuestraFormatoSinOffset_CuandoAmbosOffset0()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(18, 0), new TimeOnly(20, 0));

        extra.ToString().Should().Be("(18:00-20:00)");
    }

    [Fact]
    public void ToString_MuestraOffsetEnAmbos_CuandoAmbosOffsetsSonUno()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        extra.ToString().Should().Be("(04:00+1-06:00+1)");
    }

    [Fact]
    public void ToString_MuestraOffsetSoloEnFin_CuandoCruzaMedianoche()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(23, 0), new TimeOnly(1, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        extra.ToString().Should().Be("(23:00-01:00+1)");
    }

    // ---------- Duracion cruzando medianoche ----------

    [Fact]
    public void DuracionEnMinutos_Retorna120_CuandoExtraCruzaMedianoche()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(23, 0), new TimeOnly(1, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        extra.DuracionEnMinutos().Should().Be(120);
    }
}
