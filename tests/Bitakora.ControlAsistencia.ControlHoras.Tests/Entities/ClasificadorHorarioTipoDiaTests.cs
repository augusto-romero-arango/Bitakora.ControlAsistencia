using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ClasificadorHorarioTipoDiaTests
{
    private static readonly DateOnly FechaAnclaLunes = new(2026, 3, 16);    // lunes habil
    private static readonly DateOnly FechaAnclaSabado = new(2026, 3, 14);   // sabado
    private static readonly DateOnly FechaAnclaDomingo = new(2026, 3, 15);  // domingo

    private static readonly MomentoDelDia Momento08h = new(new TimeOnly(8, 0));

    [Fact]
    public void ClasificarTipoDia_RetornaHabil_CuandoFechaEsLunesNoFestivo()
    {
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaLunes, _ => false);

        resultado.Should().Be(TipoDia.Habil);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaHabil_CuandoFechaEsSabadoNoFestivo()
    {
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaSabado, _ => false);

        resultado.Should().Be(TipoDia.Habil);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFechaEsDomingoNoFestivo()
    {
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaDomingo, _ => false);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFechaEsLunesFestivo()
    {
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaLunes, _ => true);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFechaEsDomingoYFestivo()
    {
        // domingo || festivo: no se suma doble recargo.
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaDomingo, _ => true);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoDiaOffsetApuntaAlDomingoSiguiente()
    {
        // La evaluacion ocurre en la fecha resuelta (ancla + offset), no en la fecha ancla.
        var momentoMadrugada = new MomentoDelDia(new TimeOnly(2, 0), 1);

        var resultado = ClasificadorHorario.ClasificarTipoDia(momentoMadrugada, FechaAnclaSabado, _ => false);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFestivoCaeEnLaFechaResueltaPorOffset()
    {
        // El stub marca festivo solo el sabado resuelto: si se consultara el ancla viernes, daria Habil.
        var anclaViernes = new DateOnly(2026, 3, 13);
        var sabadoResuelto = new DateOnly(2026, 3, 14);
        var momentoMadrugada = new MomentoDelDia(new TimeOnly(2, 0), 1);

        var resultado = ClasificadorHorario.ClasificarTipoDia(
            momentoMadrugada, anclaViernes, fecha => fecha == sabadoResuelto);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }
}
