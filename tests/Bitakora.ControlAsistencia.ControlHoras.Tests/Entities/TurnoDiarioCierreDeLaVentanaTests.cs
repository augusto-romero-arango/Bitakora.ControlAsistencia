using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class TurnoDiarioCierreDeLaVentanaTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static DateTime En(int dia, int h, int m) => new(2026, 3, dia, h, m, 0);

    private static FranjaProgramada Franja(int hi, int mi, int hf, int mf, int offset = 0) =>
        new(new TimeOnly(hi, mi), new TimeOnly(hf, mf), offset, [], [], "");

    [Fact]
    public void CierreDeLaVentana_EsLaApertura_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja(7, 0, 16, 0)], "");

        turno.CierreDeLaVentana(Fecha).Should().Be(En(15, 7, 0));
    }

    [Fact]
    public void CierreDeLaVentana_SumaLaAmplitudALaApertura_CuandoTieneVentana()
    {
        var turno = new TurnoDiario("Abierto", [Franja(4, 0, 10, 0)], "", 840);

        turno.CierreDeLaVentana(Fecha).Should().Be(En(15, 18, 0));
    }

    [Fact]
    public void CierreDeLaVentana_UsaLaMenorApertura_CuandoTurnoPartidoConVentana()
    {
        var turno = new TurnoDiario("Partido", [Franja(14, 0, 18, 0), Franja(6, 0, 12, 0)], "", 120);

        turno.CierreDeLaVentana(Fecha).Should().Be(En(15, 8, 0));
    }

    [Fact]
    public void CierreDeLaVentana_EsNulo_CuandoNoHayFranjas()
    {
        var turno = new TurnoDiario("Libre", [], "", 120);

        turno.CierreDeLaVentana(Fecha).Should().BeNull();
    }
}
