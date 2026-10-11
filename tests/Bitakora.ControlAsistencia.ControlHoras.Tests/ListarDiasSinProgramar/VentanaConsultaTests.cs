using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarDiasSinProgramar;

public class VentanaConsultaTests
{
    [Fact]
    public void Recortar_RecortaA35Dias_CuandoLaVentanaLaSupera()
    {
        var ventana = VentanaConsulta.Recortar(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));

        ventana.Hasta.Should().Be(new DateOnly(2026, 11, 4));
        ventana.RangoRecortado.Should().BeTrue();
    }

    [Fact]
    public void Recortar_ConservaElHasta_CuandoLaVentanaEsDeSieteDias()
    {
        var ventana = VentanaConsulta.Recortar(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11));

        ventana.Hasta.Should().Be(new DateOnly(2026, 10, 11));
        ventana.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    public void Recortar_NoRecorta_CuandoLaVentanaEsExactamenteLaCota()
    {
        var ventana = VentanaConsulta.Recortar(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 4));

        ventana.RangoRecortado.Should().BeFalse();
    }
}
