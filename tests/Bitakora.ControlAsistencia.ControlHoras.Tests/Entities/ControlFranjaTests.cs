using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ControlFranjaTests
{
    // Descripcion (dato derivado) no interviene en estos tests: placeholder "".
    private static readonly FranjaProgramada Franja06_14 = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    private static readonly DateTime T07 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime T15 = new(2026, 3, 15, 15, 0, 0);

    [Fact]
    public void EsAnomala_EsTrue_CuandoEntradaEsNull()
    {
        var franja = new ControlFranja(Franja06_14, null, T15);

        franja.EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void EsAnomala_EsTrue_CuandoSalidaEsNull()
    {
        var franja = new ControlFranja(Franja06_14, T07, null);

        franja.EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void EsAnomala_EsTrue_CuandoEntradaYSalidaSonNull()
    {
        var franja = new ControlFranja(Franja06_14, null, null);

        franja.EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void EsAnomala_EsFalse_CuandoEntradaYSalidaEstanPobladas()
    {
        var franja = new ControlFranja(Franja06_14, T07, T15);

        franja.EsAnomala.Should().BeFalse();
    }
}
