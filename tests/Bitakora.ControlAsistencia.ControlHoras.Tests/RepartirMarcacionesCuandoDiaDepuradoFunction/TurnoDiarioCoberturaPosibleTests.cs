using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class TurnoDiarioCoberturaPosibleTests
{
    private static readonly DateOnly Dia20 = new(2026, 4, 20);

    private static TurnoDiario Turno(TimeOnly inicio, TimeOnly fin, int offsetFin, int amplitud = 0) =>
        new("Turno", [new FranjaProgramada(inicio, fin, offsetFin, [], [], string.Empty)], string.Empty, amplitud);

    [Fact]
    public void CoberturaPosible_VaDesdeDosHorasAntesHastaCuatroDespues_CuandoNoHayVentana()
    {
        var cobertura = Turno(new TimeOnly(22, 0), new TimeOnly(6, 0), 1).CoberturaPosible(Dia20);

        cobertura.Should().NotBeNull();
        cobertura!.Inicio.Should().Be(new DateTime(2026, 4, 20, 20, 0, 0));
        cobertura.Fin.Should().Be(new DateTime(2026, 4, 21, 10, 0, 0));
    }

    [Fact]
    public void CoberturaPosible_ExtiendeElFinPorLaAmplitudDeLaVentana_CuandoHayVentana()
    {
        var cobertura = Turno(new TimeOnly(20, 0), new TimeOnly(4, 0), 1, amplitud: 180).CoberturaPosible(Dia20);

        cobertura.Should().NotBeNull();
        cobertura!.Inicio.Should().Be(new DateTime(2026, 4, 20, 18, 0, 0));
        cobertura.Fin.Should().Be(new DateTime(2026, 4, 21, 11, 0, 0));
    }

    [Fact]
    public void CoberturaPosible_EsNula_CuandoElTurnoNoTieneFranjas()
    {
        var turno = new TurnoDiario("Descanso", [], string.Empty);

        turno.CoberturaPosible(Dia20).Should().BeNull();
    }
}
