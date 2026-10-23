using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DepuradorDeMarcacionesVentanaEntradaTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static DateTime En(int h, int m) => new(2026, 3, 15, h, m, 0);
    private static MarcacionNormalizada M(int h, int m) => new(En(h, m), null);
    private static MarcacionNormalizada MSig(int h, int m) => new(new DateTime(2026, 3, 16, h, m, 0), null);

    private static FranjaProgramada Franja(int hi, int mi, int hf, int mf,
        IReadOnlyList<SubFranjaProgramada>? descansos = null,
        IReadOnlyList<SubFranjaProgramada>? extras = null) =>
        new(new TimeOnly(hi, mi), new TimeOnly(hf, mf), 0, descansos ?? [], extras ?? [], "");

    private static TurnoDiario Turno07_16(int amplitud) =>
        new("Ventana", [Franja(7, 0, 16, 0)], "", amplitud);

    [Fact]
    public void Depurar_IgnoraMarcacionDeMadrugada_CuandoAmplitudEsCero()
    {
        var franja = Franja(7, 0, 16, 0);
        var turno = new TurnoDiario("Fijo", [franja], "", 0);

        var resultado = DepuradorDeMarcaciones.Depurar(turno, Fecha, [M(1, 0), M(7, 20), M(16, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(franja, En(7, 20), En(16, 0)) },
            o => o.WithStrictOrdering());
        resultado[0].CalcularDesglose(Fecha, _ => false)!.Retardo.RetardoNeto.Should().Be(20);
    }

    [Fact]
    public void Depurar_DesplazaFranjaALaEntrada_CuandoEntradaDentroDeLaVentana()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha, [M(9, 5), M(18, 5)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(9, 5, 18, 5), En(9, 5), En(18, 5)) },
            o => o.WithStrictOrdering());
        resultado[0].CalcularDesglose(Fecha, _ => false)!.Retardo.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Depurar_AcotaDesplazamientoAlCierreDeLaVentana_CuandoEntradaTardia()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha, [M(10, 20), M(19, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(10, 0, 19, 0), En(10, 20), En(19, 0)) },
            o => o.WithStrictOrdering());
        resultado[0].CalcularDesglose(Fecha, _ => false)!.Retardo.RetardoNeto.Should().Be(20);
    }

    [Fact]
    public void Depurar_NoDesplazaAntesDeLaApertura_CuandoEntradaTemprana()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha, [M(6, 30), M(16, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(7, 0, 16, 0), En(6, 30), En(16, 0)) },
            o => o.WithStrictOrdering());
        resultado[0].CalcularDesglose(Fecha, _ => false)!.Retardo.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Depurar_IgnoraMarcacionDeMadrugada_CuandoAmplitudMayorACero()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha,
            [M(1, 0), M(10, 20), M(19, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(10, 0, 19, 0), En(10, 20), En(19, 0)) },
            o => o.WithStrictOrdering());
        resultado[0].CalcularDesglose(Fecha, _ => false)!.Retardo.RetardoNeto.Should().Be(20);
    }

    [Fact]
    public void Depurar_MantieneHorarioDeReferencia_CuandoNoHayMarcacionEnLaCobertura()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha, [M(1, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(7, 0, 16, 0), null, null) },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_DesplazaTodasLasFranjasConDescansosYExtras_CuandoTurnoPartido()
    {
        var descanso = new SubFranjaProgramada(new TimeOnly(8, 0), new TimeOnly(8, 30), 0, 0, "");
        var extra = new SubFranjaProgramada(new TimeOnly(9, 0), new TimeOnly(10, 0), 0, 0, "");
        var turno = new TurnoDiario("Partido",
            [Franja(6, 0, 10, 0, [descanso], [extra]), Franja(14, 0, 18, 0)], "", 120);

        var resultado = DepuradorDeMarcaciones.Depurar(turno, Fecha,
            [M(7, 30), M(11, 35), M(15, 40), M(19, 30)]);

        var descansoEsperado = new SubFranjaProgramada(new TimeOnly(9, 30), new TimeOnly(10, 0), 0, 0, "");
        var extraEsperado = new SubFranjaProgramada(new TimeOnly(10, 30), new TimeOnly(11, 30), 0, 0, "");
        resultado.Should().BeEquivalentTo(new[]
        {
            new ControlFranja(Franja(7, 30, 11, 30, [descansoEsperado], [extraEsperado]), En(7, 30), En(11, 35)),
            new ControlFranja(Franja(15, 30, 19, 30), En(15, 40), En(19, 30))
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_AcotaDesplazamientoA119Minutos_CuandoUltimaFranjaDesbordariaElDia()
    {
        var turno = new TurnoDiario("Viola", [Franja(6, 0, 10, 0), Franja(22, 0, 23, 0)], "", 180);

        var resultado = DepuradorDeMarcaciones.Depurar(turno, Fecha, [M(9, 0)]);

        resultado.Select(c => c.Programada).Should().BeEquivalentTo(
            new[] { Franja(7, 59, 11, 59), Franja(23, 59, 0, 59) with { DiaOffsetFin = 1 } },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_AnclaEnLaPrimeraMarcacionDeLaCobertura_CuandoHayMarcacionPrevia()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha,
            [M(4, 30), M(9, 10), M(18, 10)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(9, 10, 18, 10), En(9, 10), En(18, 10)) },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_TomaSalidaPosteriorALaCoberturaDeReferencia_CuandoElHorarioSeDesplaza()
    {
        var resultado = DepuradorDeMarcaciones.Depurar(Turno07_16(180), Fecha, [M(10, 0), M(21, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja(10, 0, 19, 0), En(10, 0), En(21, 0)) },
            o => o.WithStrictOrdering());
    }
}
