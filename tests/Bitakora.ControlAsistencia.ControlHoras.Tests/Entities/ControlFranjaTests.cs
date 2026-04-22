// Issue #122: Extraer depurador de marcaciones contra franjas del turno

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de ControlFranja - solo verifica el calculo de EsAnomala (CA-9).
/// La logica de asignacion de Entrada/Salida se verifica en DepuradorDeMarcacionesTests.
/// </summary>
public class ControlFranjaTests
{
    private static readonly DetalleFranjaOrdinaria Franja06_14 = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], []);

    private static readonly DateTime T07 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime T15 = new(2026, 3, 15, 15, 0, 0);

    [Fact]
    public void EsAnomala_EsTrue_CuandoEntradaEsNull()
    {
        // CA-9: Entrada null con Salida poblada -> EsAnomala true
        var franja = new ControlFranja(Franja06_14, null, T15);

        franja.EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void EsAnomala_EsTrue_CuandoSalidaEsNull()
    {
        // CA-9: Entrada poblada con Salida null -> EsAnomala true
        var franja = new ControlFranja(Franja06_14, T07, null);

        franja.EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void EsAnomala_EsTrue_CuandoEntradaYSalidaSonNull()
    {
        // CA-9: Ambas null -> EsAnomala true
        var franja = new ControlFranja(Franja06_14, null, null);

        franja.EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void EsAnomala_EsFalse_CuandoEntradaYSalidaEstanPobladas()
    {
        // CA-9: Ambas pobladas -> EsAnomala false
        var franja = new ControlFranja(Franja06_14, T07, T15);

        franja.EsAnomala.Should().BeFalse();
    }
}
