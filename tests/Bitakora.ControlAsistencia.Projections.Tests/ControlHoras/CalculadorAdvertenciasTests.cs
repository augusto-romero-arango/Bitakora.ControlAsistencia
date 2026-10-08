// Calculo puro de advertencias; oraculos armados a mano (MEF-ADR-0002).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class CalculadorAdvertenciasTests
{
    private static readonly DateOnly Lunes = new(2026, 10, 5);
    private static readonly JornadaAplicada Jornada = new(Guid.NewGuid(), 2520, 480, 240, 1);

    private static CasillaDia C(int i, TipoCasilla tipo, int minutos) =>
        new(Lunes.AddDays(i), tipo, tipo == TipoCasilla.Trabajo ? "T" : "", minutos);

    private static List<CasillaDia> Semana(params (TipoCasilla Tipo, int Minutos)[] dias)
    {
        var l = dias.Select((d, i) => C(i, d.Tipo, d.Minutos)).ToList();
        for (var i = l.Count; i < 7; i++) l.Add(C(i, TipoCasilla.SinProgramar, 0));
        return l;
    }

    private static (TipoCasilla, int) T(int m) => (TipoCasilla.Trabajo, m);
    private static (TipoCasilla, int) D() => (TipoCasilla.Descanso, 0);
    private static (TipoCasilla, int) A() => (TipoCasilla.Ausencia, 0);

    private static AdvertenciaDiaria Diaria(TipoAdvertenciaDiaria t, int m) => new(t, m);
    private static AdvertenciaSemanal Semanal(TipoAdvertenciaSemanal t, int m) => new(t, m);

    // CA-1
    [Fact]
    public void Calcular_SuperaTopeDiario_CuandoTrabajoSuperaElTopeEnSemanaJuzgable()
    {
        var casillas = Semana(T(600), T(420), T(420), T(420), T(420), T(420), D());

        var r = CalculadorAdvertencias.Calcular(casillas, Jornada, true);

        r.Diarias[0].Should().Equal(Diaria(TipoAdvertenciaDiaria.SuperaTopeDiario, 120));
    }

    [Fact]
    public void Calcular_SuperaTopeDiario_CuandoTrabajoSuperaElTopeEnSemanaNoJuzgable()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(600), T(420)), Jornada, false);

        r.Diarias[0].Should().Equal(Diaria(TipoAdvertenciaDiaria.SuperaTopeDiario, 120));
    }

    [Fact]
    public void Calcular_SinAdvertenciaDiaria_CuandoTrabajoIgualAlTope()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(480)), Jornada, false);

        r.Diarias[0].Should().BeEmpty();
    }

    [Fact]
    public void Calcular_SinAdvertenciaDiaria_CuandoCasillaNoEsTrabajo()
    {
        var casillas = Semana(D(), A());

        var r = CalculadorAdvertencias.Calcular(casillas, Jornada, false);

        r.Diarias.Should().OnlyContain(d => d.Count == 0);
    }

    // CA-2
    [Fact]
    public void Calcular_PorDebajoDelMinimoDiario_CuandoTrabajoMenorAlMinimo()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(120), T(240)), Jornada, false);

        r.Diarias[0].Should().Equal(Diaria(TipoAdvertenciaDiaria.PorDebajoDelMinimoDiario, 120));
        r.Diarias[1].Should().BeEmpty();
    }

    [Fact]
    public void Calcular_SinAdvertenciaDiaria_CuandoMinimoEsCero()
    {
        var jornada = Jornada with { MinimoDiarioEnMinutos = 0 };

        var r = CalculadorAdvertencias.Calcular(Semana(T(60)), jornada, false);

        r.Diarias[0].Should().BeEmpty();
    }

    // CA-3
    [Fact]
    public void Calcular_SuperaHorasSemanales_CuandoJuzgableYTotalMayor()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(480), T(480), T(480), T(480), T(480), T(480), D()), Jornada, true);

        r.Semanales.Should().Equal(Semanal(TipoAdvertenciaSemanal.SuperaHorasSemanales, 360));
    }

    [Fact]
    public void Calcular_PorDebajoDeHorasSemanales_CuandoJuzgableYTotalMenor()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(380), T(380), T(380), T(380), T(380), T(380), D()), Jornada, true);

        r.Semanales.Should().Equal(Semanal(TipoAdvertenciaSemanal.PorDebajoDeHorasSemanales, 240));
    }

    [Fact]
    public void Calcular_SinAdvertenciaSemanal_CuandoJuzgableYTotalExacto()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(420), T(420), T(420), T(420), T(420), T(420), D()), Jornada, true);

        r.Semanales.Should().BeEmpty();
    }

    // CA-4
    [Fact]
    public void Calcular_SuperaHorasSemanales_CuandoNoJuzgableYTotalMayor()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(720), T(720), T(720), T(720)), Jornada, false);

        r.Semanales.Should().Equal(Semanal(TipoAdvertenciaSemanal.SuperaHorasSemanales, 360));
    }

    [Fact]
    public void Calcular_SinAdvertenciaSemanal_CuandoNoJuzgableYTotalMenor()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(360), T(360), T(360), T(360)), Jornada, false);

        r.Semanales.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_SuperaHorasSemanales_CuandoNoJuzgablePorAusenciaYTotalMayor()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(720), T(720), T(720), T(720), D(), D(), A()), Jornada, false);

        r.Semanales.Should().Equal(Semanal(TipoAdvertenciaSemanal.SuperaHorasSemanales, 360));
    }

    // CA-5
    [Fact]
    public void Calcular_FaltanDiasDeDescanso_CuandoJuzgableSinDescansos()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(360), T(360), T(360), T(360), T(360), T(360), T(360)), Jornada, true);

        r.Semanales.Should().Equal(Semanal(TipoAdvertenciaSemanal.FaltanDiasDeDescanso, 1));
    }

    [Fact]
    public void Calcular_SobranDiasDeDescanso_CuandoJuzgableConMasDescansos()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(504), T(504), T(504), T(504), T(504), D(), D()), Jornada, true);

        r.Semanales.Should().Equal(Semanal(TipoAdvertenciaSemanal.SobranDiasDeDescanso, 1));
    }

    [Fact]
    public void Calcular_SinAdvertenciaDeDescansos_CuandoJornadaNoLosControla()
    {
        var jornada = Jornada with { DiasDescansoPorSemana = 0 };

        var r = CalculadorAdvertencias.Calcular(Semana(T(630), T(630), T(630), T(630), D(), D(), D()), jornada, true);

        r.Semanales.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_SinAdvertenciaDeDescansos_CuandoNoJuzgable()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(420), T(420), T(420), T(420), T(420), T(420)), Jornada, false);

        r.Semanales.Should().BeEmpty();
    }

    // CA-6
    [Fact]
    public void Calcular_SinAdvertencias_CuandoSemanaSinJornada()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(600)), null, false);

        r.Semanales.Should().BeEmpty();
        r.Diarias.Should().OnlyContain(d => d.Count == 0);
        r.TieneAdvertencias.Should().BeFalse();
    }

    [Fact]
    public void Calcular_TieneAdvertencias_CuandoHayAlgunaDiaria()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(600)), Jornada, false);

        r.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Calcular_TieneAdvertencias_CuandoHayAlgunaSemanalSinDiarias()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(360), T(360), T(360), T(360), T(360), T(360), T(360)), Jornada, true);

        r.Diarias.Should().OnlyContain(d => d.Count == 0);
        r.TieneAdvertencias.Should().BeTrue();
    }

    [Fact]
    public void Calcular_NoTieneAdvertencias_CuandoTodoCumple()
    {
        var r = CalculadorAdvertencias.Calcular(Semana(T(420), T(420), T(420), T(420), T(420), T(420), D()), Jornada, true);

        r.TieneAdvertencias.Should().BeFalse();
    }
}
