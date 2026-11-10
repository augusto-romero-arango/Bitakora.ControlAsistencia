// Redundante a proposito con las familias sinteticas: si cambia la forma de los DesgloseFranja que
// produce ControlFranja.CalcularDesglose, las sinteticas siguen verdes y este test falla.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasPuenteTests
{
    private static readonly DateOnly Lunes = new(2026, 3, 16); // habil no festivo
    private static readonly Func<DateOnly, bool> NingunFestivo = _ => false;

    private static DateTime Dt(int hora, int minuto = 0) => new(2026, 3, 16, hora, minuto, 0);

    [Fact]
    public void Consolidar_ProduceElMismoResultadoQueLosSinteticos_CuandoLasFranjasVienenDeCalcularDesglose()
    {
        // Mismo escenario que el primer caso de ConsolidadorDesgloseHorasBasicoTests, con el codigo real.
        var controlFranja1 = new ControlFranja(Programada(8, 12), Dt(8, 30), Dt(12));
        var controlFranja2 = new ControlFranja(Programada(14, 18), Dt(14), Dt(18, 30));

        var desgloseFranja1 = controlFranja1.CalcularDesglose(Lunes, NingunFestivo);
        var desgloseFranja2 = controlFranja2.CalcularDesglose(Lunes, NingunFestivo);

        desgloseFranja1.Should().NotBeNull();
        desgloseFranja2.Should().NotBeNull();

        var resultado = ConsolidadorDesgloseHoras.Consolidar(
            [desgloseFranja1!, desgloseFranja2!], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(18), M(18, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
        });
    }
}
