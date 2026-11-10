// HorasLiquidables es el unico punto de conversion de minutos enteros a horas decimales liquidables;
// la precision (2 posiciones) vive en una constante nombrada del tipo, nunca en un literal suelto.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class HorasLiquidablesTests
{
    [Fact]
    public void DesdeMinutos_Convierte90MinutosA1Punto50Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(90);

        resultado.Should().Be(1.50m);
    }

    // 50/60 = 0.8333...: redondeo a 2 posiciones, sin midpoint exacto alcanzable con /60.
    [Fact]
    public void DesdeMinutos_Convierte50MinutosA0Punto83Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(50);

        resultado.Should().Be(0.83m);
    }

    [Fact]
    public void DesdeMinutos_Convierte0MinutosA0Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(0);

        resultado.Should().Be(0m);
    }

    // decimal, nunca double: evita el error de representacion binaria en un valor de nomina.
    [Fact]
    public void DesdeMinutos_RetornaTipoDecimal()
    {
        var resultado = HorasLiquidables.DesdeMinutos(60);

        resultado.Should().BeOfType(typeof(decimal));
        resultado.Should().Be(1m);
    }

    // Oraculo independiente registrado a mano (5/60 = 0.08333...).
    [Fact]
    public void DesdeMinutos_Convierte5MinutosA0Punto08Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(5);

        resultado.Should().Be(0.08m);
    }
}
