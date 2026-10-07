using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class LimitesJornadaTests
{
    private static LimitesJornada Crear(int semanales, int tope, int minimo, int descansos,
        int minutosSemanales = 0, int minutosTope = 0) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, minutosSemanales),
            HorasYMinutos.Crear(tope, minutosTope), HorasYMinutos.Crear(minimo, 0), descansos);

    [Fact]
    public void Crear_AceptaLimitesHabitualesYBordesInclusivos()
    {
        Crear(42, 8, 0, 1).ToString().Should().Contain("42 h semanales");
        Crear(42, 9, 4, 0).ToString().Should().Contain("sin control de descansos");
        Crear(48, 8, 8, 1).ToString().Should().Contain("48 h semanales");
        Crear(24, 24, 24, 6).ToString().Should().Contain("24 h semanales");
        Crear(1, 1, 0, 0).ToString().Should().Contain("sin control de descansos");
    }

    [Fact]
    public void ToString_DescribeTodosLosLimites_CuandoHayMinimoYUnDescanso()
    {
        Crear(42, 8, 4, 1).ToString().Should().Be(
            "42 h semanales, tope diario 8 h, mínimo diario 4 h, 1 día de descanso por semana");
    }

    [Fact]
    public void ToString_DescribeAusenciaDeMinimoYControlDeDescansos()
    {
        Crear(40, 8, 0, 2).ToString().Should().Be(
            "40 h semanales, tope diario 8 h, sin mínimo diario, 2 días de descanso por semana");
        Crear(42, 8, 0, 0).ToString().Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, sin control de descansos");
    }

    [Fact]
    public void Crear_RechazaTopesFueraDeRango()
    {
        foreach (var (tope, minutos) in new[] { (0, 0), (24, 1) })
            VerificarError(() => Crear(42, tope, 0, 1, minutosTope: minutos),
                LimitesJornada.Mensajes.TopeDiarioFueraDeRango);
    }

    [Fact]
    public void Crear_RechazaMinimoMayorQueTope()
        => VerificarError(() => Crear(42, 8, 9, 1), LimitesJornada.Mensajes.MinimoMayorQueTope);

    [Fact]
    public void Crear_RechazaDescansosFueraDeRango()
    {
        foreach (var descansos in new[] { -1, 7 })
            VerificarError(() => Crear(42, 8, 0, descansos), LimitesJornada.Mensajes.DescansosFueraDeRango);
    }

    [Fact]
    public void Crear_RechazaCeroHorasSemanales()
        => VerificarError(() => Crear(0, 8, 0, 1), LimitesJornada.Mensajes.HorasSemanalesEnCero);

    [Fact]
    public void Crear_RechazaHorasQueNoCabenEnLosDiasDisponibles()
    {
        VerificarError(() => Crear(48, 8, 0, 1, minutosSemanales: 1),
            LimitesJornada.Mensajes.HorasSemanalesExcedenCapacidad);
        VerificarError(() => Crear(42, 8, 0, 2),
            LimitesJornada.Mensajes.HorasSemanalesExcedenCapacidad);
    }

    [Fact]
    public void Crear_AcumulaViolacionesSimultaneas()
    {
        var act = () => Crear(0, 0, 9, 7);
        var mensajes = act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions.Select(e => e.Message);
        mensajes.Should().Contain(LimitesJornada.Mensajes.TopeDiarioFueraDeRango);
        mensajes.Should().Contain(LimitesJornada.Mensajes.MinimoMayorQueTope);
        mensajes.Should().Contain(LimitesJornada.Mensajes.DescansosFueraDeRango);
        mensajes.Should().Contain(LimitesJornada.Mensajes.HorasSemanalesEnCero);
    }

    private static void VerificarError(Action act, string mensaje) =>
        act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions
            .Select(e => e.Message).Should().Contain(mensaje);
}
