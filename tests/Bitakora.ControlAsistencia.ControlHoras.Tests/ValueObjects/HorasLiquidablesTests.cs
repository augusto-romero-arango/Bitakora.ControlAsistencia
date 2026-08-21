// Issue #424: HorasLiquidables es la frontera de idiomas del BC -- el unico punto de conversion de
// minutos enteros (mundo maquina) a horas decimales liquidables (mundo humano). CA-1: la precision
// (2 posiciones) vive en una constante nombrada del tipo, nunca en un literal suelto en la conversion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class HorasLiquidablesTests
{
    // CA-1: 90 minutos son 1 hora 30 minutos exactos -> 1.50m.
    [Fact]
    public void DesdeMinutos_Convierte90MinutosA1Punto50Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(90);

        resultado.Should().Be(1.50m);
    }

    // CA-1: 50 minutos no caen en una fraccion exacta de hora (50/60 = 0.8333...) -> redondeo a 2
    // posiciones decimales, sin midpoint exacto alcanzable con /60 (nota del issue).
    [Fact]
    public void DesdeMinutos_Convierte50MinutosA0Punto83Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(50);

        resultado.Should().Be(0.83m);
    }

    // CA-1: cero minutos son cero horas liquidables.
    [Fact]
    public void DesdeMinutos_Convierte0MinutosA0Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(0);

        resultado.Should().Be(0m);
    }

    // CA-1: el tipo de retorno es decimal, nunca double (evita el error de representacion binaria de
    // fracciones que double introduciria en un valor de nomina).
    [Fact]
    public void DesdeMinutos_RetornaTipoDecimal()
    {
        var resultado = HorasLiquidables.DesdeMinutos(60);

        resultado.Should().BeOfType(typeof(decimal));
        resultado.Should().Be(1m);
    }

    // CA-1: 5 minutos de una franja completa (usado en escenarios de excedente del dominio) -> 0.08m,
    // oraculo independiente registrado a mano (5/60 = 0.08333...).
    [Fact]
    public void DesdeMinutos_Convierte5MinutosA0Punto08Horas()
    {
        var resultado = HorasLiquidables.DesdeMinutos(5);

        resultado.Should().Be(0.08m);
    }
}
