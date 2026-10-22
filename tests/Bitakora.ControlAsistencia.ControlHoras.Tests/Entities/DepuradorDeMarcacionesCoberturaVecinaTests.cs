using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DepuradorDeMarcacionesCoberturaVecinaTests
{
    private static readonly DateOnly Vispera = new(2026, 4, 20);
    private static readonly DateOnly Dia = new(2026, 4, 21);

    private static readonly FranjaProgramada Franja17_01 = new(new TimeOnly(17, 0), new TimeOnly(1, 0), 1, [], [], "");
    private static readonly FranjaProgramada Franja06_14 = new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja07_16 = new(new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja20_04 = new(new TimeOnly(20, 0), new TimeOnly(4, 0), 1, [], [], "");
    private static readonly FranjaProgramada Franja09_17 = new(new TimeOnly(9, 0), new TimeOnly(17, 0), 0, [], [], "");

    private static MarcacionNormalizada M(DateOnly fecha, int h, int m) =>
        new(fecha.ToDateTime(new TimeOnly(h, m)), null);

    private static DateTime En(DateOnly fecha, int h, int m) => fecha.ToDateTime(new TimeOnly(h, m));

    // CA-1: la 04:30 cae en el solape (vispera hasta 05:00, dia desde 04:00): ninguno la usa.
    [Fact]
    public void Depurar_DejaLaVisperaSinSalida_CuandoLaMarcacionEsAmbigua()
    {
        var turno = new TurnoDiario("Noche", [Franja17_01], "");
        var vecinas = new CoberturasVecinas(null, En(Dia, 4, 0));

        var resultado = DepuradorDeMarcaciones.Depurar(
            turno, Vispera, [M(Vispera, 16, 55), M(Dia, 4, 30)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja17_01, En(Vispera, 16, 55), null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void Depurar_TomaLaEntradaDe0620_CuandoLa0430EsAmbigua()
    {
        var turno = new TurnoDiario("Dia", [Franja06_14], "");
        var vecinas = new CoberturasVecinas(En(Dia, 5, 0), null);

        var resultado = DepuradorDeMarcaciones.Depurar(
            turno, Dia, [M(Dia, 4, 30), M(Dia, 6, 20)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(Dia, 6, 20), null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    // CA-2 (lado depurador): con el inicio del dia siguiente en 06:00 la vispera toma la 04:30.
    [Fact]
    public void Depurar_TomaLa0430ComoSalida_CuandoElDiaSiguienteEmpiezaSuCoberturaAlas0600()
    {
        var turno = new TurnoDiario("Noche", [Franja17_01], "");
        var vecinas = new CoberturasVecinas(null, En(Dia, 6, 0));

        var resultado = DepuradorDeMarcaciones.Depurar(
            turno, Vispera, [M(Vispera, 16, 55), M(Dia, 4, 30)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja17_01, En(Vispera, 16, 55), En(Dia, 4, 30)) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeFalse();
    }

    // CA-3: sin solape (dia desde 05:00) la 04:30 es salida de la vispera.
    [Fact]
    public void Depurar_TomaLa0430ComoSalidaDeLaVispera_CuandoNoHaySolape()
    {
        var turno = new TurnoDiario("Noche", [Franja17_01], "");
        var vecinas = new CoberturasVecinas(null, En(Dia, 5, 0));

        var resultado = DepuradorDeMarcaciones.Depurar(
            turno, Vispera, [M(Vispera, 16, 55), M(Dia, 4, 30)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja17_01, En(Vispera, 16, 55), En(Dia, 4, 30)) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeFalse();
    }

    [Fact]
    public void Depurar_NoUsaLa0430EnElDia_CuandoCaeFueraDeSuCoberturaYDentroDeLaDeLaVispera()
    {
        var turno = new TurnoDiario("Dia", [Franja07_16], "");
        var vecinas = new CoberturasVecinas(En(Dia, 5, 0), null);

        var resultado = DepuradorDeMarcaciones.Depurar(
            turno, Dia, [M(Dia, 4, 30), M(Dia, 7, 0)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja07_16, En(Dia, 7, 0), null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    // CA-4: la vispera entro a las 23:00 (cobertura hasta 11:00): la 08:55 es ambigua.
    [Fact]
    public void Depurar_DejaLaFranjaDelDiaSinMarcaciones_CuandoLa0855EsAmbigua()
    {
        var turno = new TurnoDiario("Dia", [Franja09_17], "");
        var vecinas = new CoberturasVecinas(En(Dia, 11, 0), null);

        var resultado = DepuradorDeMarcaciones.Depurar(turno, Dia, [M(Dia, 8, 55)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja09_17, null, null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void Depurar_ExcluyeLa0855DeLaVispera_CuandoCaeEnLaCoberturaDelDiaSiguiente()
    {
        var turno = new TurnoDiario("Noche con ventana", [Franja20_04], "", 180);
        var vecinas = new CoberturasVecinas(null, En(Dia, 7, 0));

        var resultado = DepuradorDeMarcaciones.Depurar(
            turno, Vispera, [M(Vispera, 23, 0), M(Dia, 8, 55)], vecinas);

        resultado.Should().HaveCount(1);
        resultado[0].Entrada.Should().Be(En(Vispera, 23, 0));
        resultado[0].Salida.Should().BeNull();
    }

    // CA-4: la vispera entro a las 20:00 (cobertura hasta 08:00): la 08:55 es la entrada del dia.
    [Fact]
    public void Depurar_TomaLa0855ComoEntradaDelDia_CuandoLaCoberturaDeLaVisperaTerminaAlas0800()
    {
        var turno = new TurnoDiario("Dia", [Franja09_17], "");
        var vecinas = new CoberturasVecinas(En(Dia, 8, 0), null);

        var resultado = DepuradorDeMarcaciones.Depurar(turno, Dia, [M(Dia, 8, 55)], vecinas);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja09_17, En(Dia, 8, 55), null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }
}
