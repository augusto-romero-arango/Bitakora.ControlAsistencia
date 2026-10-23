using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DepuradorDeMarcacionesTests
{
    private static readonly FranjaProgramada Franja06_14 = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja06_12 = new(
        new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja07_16 = new(
        new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja14_18 = new(
        new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja22_06_Nocturna = new(
        new TimeOnly(22, 0), new TimeOnly(6, 0), 1, [], [], "");

    private static readonly DateOnly FechaBase = new(2026, 3, 15);

    private static DateTime En(int h, int m) => new(2026, 3, 15, h, m, 0);
    private static DateTime EnDiaSiguiente(int h, int m) => new(2026, 3, 16, h, m, 0);

    private static readonly MarcacionNormalizada Marcacion05_50 = new(En(5, 50), null);
    private static readonly MarcacionNormalizada Marcacion12_05 = new(En(12, 5), null);

    private static MarcacionNormalizada M(int h, int m) => new(En(h, m), null);

    private static MarcacionNormalizada MSig(int h, int m) => new(EnDiaSiguiente(h, m), null);

    [Fact]
    public void Depurar_ProduceEntradaYSalidaCorrectas_CuandoFranjaUnicaConDosMarcaciones()
    {
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(7, 0), M(15, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(7, 0), En(15, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_DistribuyeMarcacionesPorPuntoDeCorte_CuandoTurnoPartido()
    {
        var turno = new TurnoDiario("Partido", [Franja06_12, Franja14_18], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [Marcacion05_50, Marcacion12_05, M(14, 10), M(18, 15)]);

        resultado.Should().BeEquivalentTo(new[]
        {
            new ControlFranja(Franja06_12, En(5, 50), En(12, 5)),
            new ControlFranja(Franja14_18, En(14, 10), En(18, 15))
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_ProduceEntradaConSalidaNull_CuandoFranjaTieneUnaSolaMarcacion()
    {
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(7, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(7, 0), null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void Depurar_ProduceEntradaYSalidaNull_CuandoFranjaSinMarcaciones()
    {
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, []);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, null, null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void Depurar_DescartaMarcacionesIntermedias_CuandoHayMasDeDosMarcaciones()
    {
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [M(7, 0), M(10, 0), M(12, 0), M(15, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(7, 0), En(15, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_RetornaListaVacia_CuandoTurnoEsNull()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(null, FechaBase, [M(7, 0)]);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Depurar_RetornaListaVacia_CuandoTurnoNoTieneFranjas()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(new TurnoDiario("Vacio", [], ""), FechaBase, [M(7, 0)]);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Depurar_ResuelveRangoNocturno_CuandoFranjaTieneDiaOffsetFin1()
    {
        var turno = new TurnoDiario("Nocturno", [Franja22_06_Nocturna], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(22, 30), MSig(5, 30)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja22_06_Nocturna, En(22, 30), EnDiaSiguiente(5, 30)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_IgnoraMarcacionesFueraDeCobertura_CuandoHayUnaSolaFranja()
    {
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(3, 0), M(20, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, null, null) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_IgnoraMadrugadaYTomaEntradaTardia_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja07_16], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(1, 0), M(7, 20), M(16, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja07_16, En(7, 20), En(16, 0)) },
            options => options.WithStrictOrdering());
        resultado[0].CalcularDesglose(FechaBase, _ => false)!.Retardo.RetardoNeto.Should().Be(20);
    }

    [Fact]
    public void Depurar_TomaSalidaDeMadrugadaDelDiaSiguiente_CuandoNocturnosConsecutivos()
    {
        var franja = new FranjaProgramada(new TimeOnly(17, 0), new TimeOnly(1, 0), 1, [], [], "");
        var turno = new TurnoDiario("Nocturno", [franja], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(1, 0), M(17, 5), MSig(1, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(franja, En(17, 5), EnDiaSiguiente(1, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_TomaEntradaDosHorasAntesDeApertura_CuandoTurnoDeMadrugada()
    {
        var franja = new FranjaProgramada(new TimeOnly(2, 0), new TimeOnly(10, 0), 0, [], [], "");
        var turno = new TurnoDiario("Madrugada", [franja], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(1, 58), M(10, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(franja, En(1, 58), En(10, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_IgnoraEntradaUnMinutoAntesDeLaCobertura_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja07_16], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(4, 59), M(16, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja07_16, En(16, 0), null) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_TomaEntradaALasCincoEnPunto_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja07_16], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(5, 0), M(16, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja07_16, En(5, 0), En(16, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_TomaSalidaUnMinutoAntesDelFinDeCobertura_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja07_16], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(7, 0), M(19, 59)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja07_16, En(7, 0), En(19, 59)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_IgnoraSalidaAlFinDeCobertura_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja07_16], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(7, 0), M(20, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja07_16, En(7, 0), null) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_ConservaPuntoMedioInterno_CuandoTurnoPartidoConMarcacionesFueraDeCobertura()
    {
        var turno = new TurnoDiario("Partido", [Franja06_12, Franja14_18], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [M(3, 30), Marcacion05_50, Marcacion12_05, M(14, 10), M(18, 15), M(22, 30)]);

        resultado.Should().BeEquivalentTo(new[]
        {
            new ControlFranja(Franja06_12, En(5, 50), En(12, 5)),
            new ControlFranja(Franja14_18, En(14, 10), En(18, 15))
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_SegundaFranjaEsAnomala_CuandoSoloPrimeraFranjaTieneMarcaciones()
    {
        var turno = new TurnoDiario("Partido", [Franja06_12, Franja14_18], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [Marcacion05_50, Marcacion12_05]);

        resultado.Should().BeEquivalentTo(new[]
        {
            new ControlFranja(Franja06_12, En(5, 50), En(12, 5)),
            new ControlFranja(Franja14_18, null, null)
        }, options => options.WithStrictOrdering());
        resultado[1].EsAnomala.Should().BeTrue();
    }
}
