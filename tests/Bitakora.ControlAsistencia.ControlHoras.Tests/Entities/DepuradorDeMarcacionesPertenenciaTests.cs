using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DepuradorDeMarcacionesPertenenciaTests
{
    private static readonly DateOnly Vispera = new(2026, 4, 20);
    private static readonly DateOnly Dia = new(2026, 4, 21);

    private static readonly FranjaProgramada Franja17_01 = new(new TimeOnly(17, 0), new TimeOnly(1, 0), 1, [], [], "");
    private static readonly FranjaProgramada Franja06_14 = new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja07_16 = new(new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "");

    private static DateTime En(DateOnly fecha, int h, int m) => fecha.ToDateTime(new TimeOnly(h, m));

    private static MarcacionNormalizada M(DateOnly fecha, int h, int m) => new(En(fecha, h, m), null);

    [Fact]
    public void Clasificar_MarcaLa0430ComoAmbigua_CuandoCaeEnLaCoberturaDelDiaSiguiente()
    {
        var turno = new TurnoDiario("Noche", [Franja17_01], "");
        var vecinas = new CoberturasVecinas(null, En(Dia, 4, 0));

        var resultado = DepuradorDeMarcaciones.Clasificar(
            turno, Vispera, [M(Vispera, 16, 55), M(Dia, 4, 30)], vecinas);

        resultado.Should().BeEquivalentTo(new Dictionary<DateTime, PertenenciaMarcacion>
        {
            [En(Vispera, 16, 55)] = PertenenciaMarcacion.DelTurno,
            [En(Dia, 4, 30)] = PertenenciaMarcacion.Ambigua
        });
    }

    [Fact]
    public void Clasificar_MarcaLa0430ComoAmbigua_CuandoCaeEnLaCoberturaDeLaVispera()
    {
        var turno = new TurnoDiario("Dia", [Franja06_14], "");
        var vecinas = new CoberturasVecinas(En(Dia, 5, 0), null);

        var resultado = DepuradorDeMarcaciones.Clasificar(
            turno, Dia, [M(Dia, 4, 30), M(Dia, 6, 20)], vecinas);

        resultado.Should().BeEquivalentTo(new Dictionary<DateTime, PertenenciaMarcacion>
        {
            [En(Dia, 4, 30)] = PertenenciaMarcacion.Ambigua,
            [En(Dia, 6, 20)] = PertenenciaMarcacion.DelTurno
        });
    }

    [Fact]
    public void Clasificar_MarcaFueraDeCobertura_CuandoLaMarcaPasaDeFinMasCuatroHoras()
    {
        var turno = new TurnoDiario("Dia", [Franja07_16], "");

        var resultado = DepuradorDeMarcaciones.Clasificar(
            turno, Dia, [M(Dia, 7, 10), M(Dia, 12, 0), M(Dia, 16, 5), M(Dia, 20, 15)]);

        resultado.Should().BeEquivalentTo(new Dictionary<DateTime, PertenenciaMarcacion>
        {
            [En(Dia, 7, 10)] = PertenenciaMarcacion.DelTurno,
            [En(Dia, 12, 0)] = PertenenciaMarcacion.DelTurno,
            [En(Dia, 16, 5)] = PertenenciaMarcacion.DelTurno,
            [En(Dia, 20, 15)] = PertenenciaMarcacion.FueraDeCobertura
        });
    }

    [Fact]
    public void Clasificar_MarcaLa0100DelTurnoEnLaVispera_CuandoNoHaySolape()
    {
        var turno = new TurnoDiario("Noche", [Franja17_01], "");
        var vecinas = new CoberturasVecinas(null, En(Dia, 5, 0));

        var resultado = DepuradorDeMarcaciones.Clasificar(turno, Vispera, [M(Dia, 1, 0)], vecinas);

        resultado.Should().BeEquivalentTo(new Dictionary<DateTime, PertenenciaMarcacion>
        {
            [En(Dia, 1, 0)] = PertenenciaMarcacion.DelTurno
        });
    }

    [Fact]
    public void Clasificar_MarcaLa0100FueraDeCoberturaEnElDia_CuandoNoHaySolape()
    {
        var turno = new TurnoDiario("Dia", [Franja07_16], "");
        var vecinas = new CoberturasVecinas(En(Dia, 5, 0), null);

        var resultado = DepuradorDeMarcaciones.Clasificar(turno, Dia, [M(Dia, 1, 0)], vecinas);

        resultado.Should().BeEquivalentTo(new Dictionary<DateTime, PertenenciaMarcacion>
        {
            [En(Dia, 1, 0)] = PertenenciaMarcacion.FueraDeCobertura
        });
    }

    [Fact]
    public void Clasificar_NoClasifica_CuandoNoHayTurno()
    {
        var resultado = DepuradorDeMarcaciones.Clasificar(null, Dia, [M(Dia, 7, 10)]);

        resultado.Should().BeEmpty();
    }
}
