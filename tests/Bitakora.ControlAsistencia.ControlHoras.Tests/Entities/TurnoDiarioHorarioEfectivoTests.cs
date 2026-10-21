using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class TurnoDiarioHorarioEfectivoTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static FranjaProgramada Franja(int hi, int hf) =>
        new(new TimeOnly(hi, 0), new TimeOnly(hf, 0), 0, [], [], "");

    [Fact]
    public void Desplazar_CorreInicioYFin_CuandoMinutosPositivos()
    {
        Franja(7, 16).Desplazar(125).Should().Be(
            new FranjaProgramada(new TimeOnly(9, 5), new TimeOnly(18, 5), 0, [], [], ""));
    }

    [Fact]
    public void Desplazar_AjustaOffsetDeFin_CuandoFranjaCruzaMedianoche()
    {
        var franja = new FranjaProgramada(new TimeOnly(22, 0), new TimeOnly(23, 0), 0, [], [], "");

        franja.Desplazar(119).Should().Be(
            new FranjaProgramada(new TimeOnly(23, 59), new TimeOnly(0, 59), 1, [], [], ""));
    }

    [Fact]
    public void Desplazar_RecalculaOffsetsDeLaSubFranja_CuandoCruzaMedianoche()
    {
        var sub = new SubFranjaProgramada(new TimeOnly(23, 30), new TimeOnly(23, 50), 0, 0, "");

        sub.Desplazar(30).Should().Be(
            new SubFranjaProgramada(new TimeOnly(0, 0), new TimeOnly(0, 20), 1, 1, ""));
    }

    [Fact]
    public void HorarioEfectivo_DevuelveReferencia_CuandoAmplitudEsCero()
    {
        var turno = new TurnoDiario("Fijo", [Franja(7, 16)], "", 0);

        turno.HorarioEfectivo(Fecha, new DateTime(2026, 3, 15, 9, 0, 0)).Should().Be(turno);
    }

    [Fact]
    public void HorarioEfectivo_DevuelveReferencia_CuandoNoHayMarcacion()
    {
        var turno = new TurnoDiario("Ventana", [Franja(7, 16)], "", 180);

        turno.HorarioEfectivo(Fecha, null).Should().Be(turno);
    }

    [Fact]
    public void HorarioEfectivo_AcotaALaApertura_CuandoMarcacionAnterior()
    {
        var turno = new TurnoDiario("Ventana", [Franja(7, 16)], "", 180);

        turno.HorarioEfectivo(Fecha, new DateTime(2026, 3, 15, 6, 30, 0)).Should().Be(turno);
    }

    [Fact]
    public void HorarioEfectivo_AcotaAlCierre_CuandoMarcacionDelDiaSiguiente()
    {
        var turno = new TurnoDiario("Ventana", [Franja(7, 16)], "", 180);

        turno.HorarioEfectivo(Fecha, new DateTime(2026, 3, 16, 2, 0, 0)).Should().Be(
            new TurnoDiario("Ventana", [Franja(10, 19)], "", 180));
    }

    [Fact]
    public void TieneVentanaDeEntrada_EsVerdadero_CuandoAmplitudMayorACero()
    {
        new TurnoDiario("V", [Franja(7, 16)], "", 1).TieneVentanaDeEntrada.Should().BeTrue();
        new TurnoDiario("V", [Franja(7, 16)], "", 0).TieneVentanaDeEntrada.Should().BeFalse();
    }
}
