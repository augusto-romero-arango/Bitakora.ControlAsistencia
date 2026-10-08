using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class TurnoDiarioMinutosOrdinariosTests
{
    private static SubFranjaProgramada Sub(int hi, int hf) =>
        new(new TimeOnly(hi, 0), new TimeOnly(hf, 0), 0, 0, "");

    private static FranjaProgramada Franja(
        int hi, int hf, int offsetFin = 0,
        IReadOnlyList<SubFranjaProgramada>? descansos = null, IReadOnlyList<SubFranjaProgramada>? extras = null) =>
        new(new TimeOnly(hi, 0), new TimeOnly(hf, 0), offsetFin, descansos ?? [], extras ?? [], "");

    private static TurnoDiario Turno(params FranjaProgramada[] franjas) => new("T", franjas, "");

    [Fact]
    public void MinutosOrdinarios_ExcluyeElDescanso_CuandoFranjaTieneDescanso()
    {
        Turno(Franja(8, 17, descansos: [Sub(12, 13)])).MinutosOrdinarios().Should().Be(480);
    }

    [Fact]
    public void MinutosOrdinarios_CuentaTurnoCompleto_CuandoCruzaLaMedianoche()
    {
        Turno(Franja(22, 6, offsetFin: 1)).MinutosOrdinarios().Should().Be(480);
    }

    [Fact]
    public void MinutosOrdinarios_ExcluyeExtras_CuandoFranjaTieneExtra()
    {
        Turno(Franja(6, 18, extras: [Sub(16, 18)])).MinutosOrdinarios().Should().Be(600);
    }

    [Fact]
    public void MinutosOrdinarios_SumaTodasLasFranjas_CuandoHayDos()
    {
        Turno(Franja(6, 10), Franja(14, 18)).MinutosOrdinarios().Should().Be(480);
    }

    [Fact]
    public void MinutosOrdinarios_EsCero_CuandoNoHayFranjas()
    {
        Turno().MinutosOrdinarios().Should().Be(0);
    }
}
