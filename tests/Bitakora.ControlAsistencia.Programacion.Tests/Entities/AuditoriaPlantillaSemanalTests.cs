using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class AuditoriaPlantillaSemanalTests
{
    private static readonly DiaSemana[] Dias =
    [
        DiaSemana.Lunes, DiaSemana.Martes, DiaSemana.Miercoles, DiaSemana.Jueves,
        DiaSemana.Viernes, DiaSemana.Sabado, DiaSemana.Domingo
    ];

    private static LimitesJornada Limites(int horasSemanales = 42, int tope = 8, int minimo = 4, int descansos = 1) =>
        LimitesJornada.Crear(
            HorasYMinutos.Crear(horasSemanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(minimo, 0), descansos);

    private static Turno Trabajo(int minutos) =>
        Turno.Crear("Turno", false,
            [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(6, 0).AddMinutes(minutos))]);

    private static Turno Descanso() => Turno.Crear("Libre", true, []);

    private static Turno Incompleto() => Turno.Crear("Nuevo", false, []);

    // Un elemento por dia ISO (lunes primero); null deja el dia ausente (vacio).
    private static List<DiaDePlantillaAuditado> Semana(int semana, params Turno?[] turnos) =>
        turnos
            .Select((turno, i) => turno is null ? null : new DiaDePlantillaAuditado(semana, Dias[i], turno, false))
            .OfType<DiaDePlantillaAuditado>()
            .ToList();

    private static Turno?[] SeisDeTrabajoYDescanso(int minutosPorDia) =>
        [.. Enumerable.Repeat(Trabajo(minutosPorDia), 6), Descanso()];

    private static IReadOnlyList<AdvertenciaPlantillaSemanal> Auditar(
        int semanas, IEnumerable<DiaDePlantillaAuditado> dias, LimitesJornada? limites) =>
        AuditoriaPlantillaSemanal.Auditar(semanas, dias, limites);

    [Fact]
    public void Auditar_DevuelveSoloPlantillaSinJornada_CuandoNoHayJornada()
    {
        var dias = Semana(1, Trabajo(700), null, Incompleto());

        var resultado = Auditar(2, dias, null);

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.PlantillaSinJornada());
    }

    [Fact]
    public void Auditar_NoAdvierteNada_CuandoLaSemanaCumpleTodo()
    {
        var resultado = Auditar(1, Semana(1, SeisDeTrabajoYDescanso(420)), Limites());

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Auditar_AdvierteDiaSinTurnoYFaltaDescanso_CuandoElDiaEstaVacio()
    {
        var resultado = Auditar(1, Semana(1, [.. Enumerable.Repeat(Trabajo(420), 6), null]), Limites());

        resultado.Should().HaveCount(2)
            .And.Contain(AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Domingo))
            .And.Contain(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1));
    }

    [Fact]
    public void Auditar_AdvierteDiaSinTurnoYFaltaDescanso_CuandoElTurnoEstaRetirado()
    {
        var dias = Semana(1, [.. Enumerable.Repeat(Trabajo(420), 6), null]);
        dias.Add(new DiaDePlantillaAuditado(1, DiaSemana.Domingo, Trabajo(420), true));

        var resultado = Auditar(1, dias, Limites());

        resultado.Should().HaveCount(2)
            .And.Contain(AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Domingo))
            .And.Contain(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1));
    }

    [Fact]
    public void Auditar_AdvierteDiaSinTurnoYFaltaDescanso_CuandoElTurnoEstaIncompleto()
    {
        var resultado = Auditar(1, Semana(1, [.. Enumerable.Repeat(Trabajo(420), 6), Incompleto()]), Limites());

        resultado.Should().HaveCount(2)
            .And.Contain(AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Domingo))
            .And.Contain(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1));
    }

    [Fact]
    public void Auditar_AdvierteSieteDiasSinTurnoYFaltantes_CuandoLaPlantillaEstaRecienCreada()
    {
        var resultado = Auditar(1, [], Limites());

        resultado.Should().HaveCount(9)
            .And.Contain(AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 2520))
            .And.Contain(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1))
            .And.Contain(AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Lunes))
            .And.Contain(AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Domingo));
    }

    [Fact]
    public void Auditar_AdvierteSuperaTopeDiario_CuandoElDiaTiene600Minutos()
    {
        var resultado = Auditar(1, Semana(1, Trabajo(600), Trabajo(384), Trabajo(384), Trabajo(384),
            Trabajo(384), Trabajo(384), Descanso()), Limites());

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Lunes, 120));
    }

    [Fact]
    public void Auditar_AdvierteDebajoDelMinimoDiario_CuandoElDiaTiene120Minutos()
    {
        var resultado = Auditar(1, Semana(1, Trabajo(120), Trabajo(480), Trabajo(480), Trabajo(480),
            Trabajo(480), Trabajo(480), Descanso()), Limites());

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.PorDebajoDelMinimoDiario(1, DiaSemana.Lunes, 120));
    }

    [Fact]
    public void Auditar_NoAdvierteTopeNiMinimo_CuandoElDiaTiene480Minutos()
    {
        var resultado = Auditar(1, Semana(1, Trabajo(480), Trabajo(408), Trabajo(408), Trabajo(408),
            Trabajo(408), Trabajo(408), Descanso()), Limites());

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Auditar_NuncaAdvierteMinimoDiario_CuandoElMinimoEsCero()
    {
        var resultado = Auditar(1, Semana(1, Trabajo(120), Trabajo(480), Trabajo(480), Trabajo(480),
            Trabajo(480), Trabajo(480), Descanso()), Limites(minimo: 0));

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Auditar_AdvierteSuperaHorasSemanales_CuandoLaSemanaCompletaSuma2880()
    {
        var resultado = Auditar(1, Semana(1, SeisDeTrabajoYDescanso(480)), Limites());

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.SuperaHorasSemanales(1, 360));
    }

    [Fact]
    public void Auditar_AdvierteDebajoDeHorasSemanales_CuandoLaSemanaCompletaSuma2280()
    {
        var resultado = Auditar(1, Semana(1, SeisDeTrabajoYDescanso(380)), Limites());

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 240));
    }

    [Fact]
    public void Auditar_AdvierteSuperaHorasSemanales_CuandoLaSemanaIncompletaExcedeElTotal()
    {
        var resultado = Auditar(1, Semana(1, Enumerable.Repeat(Trabajo(480), 5).ToArray()),
            Limites(horasSemanales: 30));

        resultado.Should().Contain(AdvertenciaPlantillaSemanal.SuperaHorasSemanales(1, 600));
    }

    [Fact]
    public void Auditar_AdvierteDebajoDeHorasSemanales_CuandoLaSemanaIncompletaQuedaCorta()
    {
        var resultado = Auditar(1, Semana(1, Enumerable.Repeat(Trabajo(456), 5).ToArray()), Limites());

        resultado.Should().Contain(AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 240));
    }

    [Fact]
    public void Auditar_AdvierteFaltanDiasDeDescanso_CuandoLaSemanaNoTieneDescansos()
    {
        var resultado = Auditar(1, Semana(1, Enumerable.Repeat(Trabajo(360), 7).ToArray()), Limites());

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1));
    }

    [Fact]
    public void Auditar_AdvierteSobranDiasDeDescanso_CuandoLaSemanaTieneDosDescansos()
    {
        var resultado = Auditar(1, Semana(1, Trabajo(504), Trabajo(504), Trabajo(504), Trabajo(504),
            Trabajo(504), Descanso(), Descanso()), Limites(tope: 9));

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.SobranDiasDeDescanso(1, 1));
    }

    [Fact]
    public void Auditar_AdvierteFaltanDiasDeDescanso_CuandoLaSemanaIncompletaNoTieneDescansos()
    {
        var resultado = Auditar(1, Semana(1, Trabajo(420), Trabajo(420), Trabajo(420)), Limites());

        resultado.Should().Contain(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1));
    }

    [Fact]
    public void Auditar_NuncaAdvierteDescansos_CuandoElParametroEsCero()
    {
        var sinDescansos = Semana(1, Enumerable.Repeat(Trabajo(360), 7).ToArray());
        var conTres = Semana(2, Trabajo(480), Trabajo(480), Trabajo(480), Trabajo(480),
            Descanso(), Descanso(), Descanso());

        var resultado = Auditar(2, [.. sinDescansos, .. conTres], Limites(descansos: 0));

        resultado.Should().NotContain(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1))
            .And.NotContain(AdvertenciaPlantillaSemanal.SobranDiasDeDescanso(2, 3));
    }

    [Fact]
    public void Auditar_JuzgaCadaSemanaPorSeparado_CuandoLaPlantillaTieneDosSemanas()
    {
        var dias = Semana(1, SeisDeTrabajoYDescanso(420))
            .Concat(Semana(2, SeisDeTrabajoYDescanso(480)))
            .ToList();

        var resultado = Auditar(2, dias, Limites());

        resultado.Should().Equal(AdvertenciaPlantillaSemanal.SuperaHorasSemanales(2, 360));
    }

    [Fact]
    public void Auditar_OrdenaPorSemanaYDiaIso_CuandoHayAdvertenciasDeVariosDias()
    {
        var dias = Semana(2, null, Trabajo(420), Trabajo(420), Trabajo(420), Trabajo(420), Trabajo(420), Descanso())
            .Concat(Semana(1, Trabajo(420), null, null, Trabajo(420), Trabajo(420), Trabajo(420), Descanso()))
            .ToList();

        var resultado = Auditar(2, dias, Limites());

        resultado.Should().ContainInOrder(
            AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Martes),
            AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Miercoles),
            AdvertenciaPlantillaSemanal.DiaSinTurno(2, DiaSemana.Lunes));
    }

    [Fact]
    public void Auditar_DevuelveListasIguales_CuandoSeAuditaDosVecesElMismoDiseno()
    {
        var dias = Semana(1, Trabajo(600), null, Incompleto(), Trabajo(120), Descanso(), Descanso(), Trabajo(300));

        var primera = Auditar(1, dias, Limites());
        var segunda = Auditar(1, dias.AsEnumerable().Reverse(), Limites());

        primera.Should().NotBeEmpty();
        primera.Should().Equal(segunda);
    }
}
