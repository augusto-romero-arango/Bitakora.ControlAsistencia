// HU-116: Consolidar DesgloseFranjas del dia con compensacion cronologica inversa.
// Familia 6 - Puente con ControlFranja.CalcularDesglose real (#136). Construye dos ControlFranja
// con DateTime?, los pasa por CalcularDesglose y entrega la lista resultante a Consolidar.
//
// Este CA es REDUNDANTE en cobertura con las familias 1-5 (replica el escenario de CA-1), pero es
// el antidoto contra divergencia silenciosa: si una refactorizacion futura de #135/#136 cambia la
// forma de los DesgloseFranja (orden de intervalos, presencia de TiempoCompensado, conceptos), las
// familias sinteticas seguiran verdes mientras este test fallara visiblemente.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasPuenteTests
{
    private static readonly DateOnly Lunes = new(2026, 3, 16); // habil no festivo
    private static readonly Func<DateOnly, bool> NingunFestivo = _ => false;

    // Marcacion como DateTime (frontera del dominio: las marcaciones llegan como DateTime).
    private static DateTime Dt(int hora, int minuto = 0) => new(2026, 3, 16, hora, minuto, 0);

    [Fact]
    public void Consolidar_ProduceElMismoResultadoQueLosSinteticos_CuandoLasFranjasVienenDeCalcularDesglose()
    {
        // Replica el ejemplo del Contexto / CA-1 con el codigo real: F1 8:00-12:00 con entrada tardia
        // 8:30 (retardo 30min, sin excedente); F2 14:00-18:00 con salida 18:30 (excedente 30min, sin
        // retardo). La compensacion cross-franja come los 30min finales del excedente (18:00-18:30):
        // RetardoNeto = 0, extras del dia = 0, ordinarias = 450min.
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
