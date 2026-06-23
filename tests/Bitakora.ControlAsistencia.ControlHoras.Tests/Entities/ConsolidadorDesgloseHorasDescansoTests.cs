// HU-116: Consolidar DesgloseFranjas del dia con compensacion cronologica inversa.
// Familia 5 - Los intervalos de concepto Descanso NUNCA son comidos por la compensacion. Solo las
// extras (todos los Extra* y los DominicalFestiva* cuando son extras) compensan el retardo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasDescansoTests
{
    [Fact]
    public void Consolidar_NoTocaElDescanso_CuandoCompensaElRetardoConElExcedente()
    {
        // CA-14: F1 con descanso 12:00-13:00 (Concepto.Descanso) y retardo 30min en la entrada (8:30);
        // F2 con excedente 30min (20:00-20:30). La compensacion consume los 30min de excedente y NO
        // toca el descanso: el TiempoCompensado contiene solo el intervalo del excedente, y el descanso
        // sigue apareciendo intacto en TotalMinutosPorConcepto[Descanso] = 60.
        var franja1 = Desglose(
            Programada(8, 17),
            [
                Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna),
                Clasif(M(12), M(13), Concepto.Descanso),
                Clasif(M(13), M(17), Concepto.OrdinariaDiurna),
            ],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(18, 20),
            [
                Clasif(M(18), M(20), Concepto.OrdinariaDiurna),
                Clasif(M(20), M(20, 30), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        // El compensado es exactamente el excedente; el descanso 12:00-13:00 jamas aparece aqui.
        resultado.RetardoTotal.Should().Be(Retardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(20), M(20, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            // F1: 8:30-12:00 (210) + 13:00-17:00 (240) = 450; F2 ordinaria: 18:00-20:00 (120) => 570.
            [Concepto.OrdinariaDiurna] = 570,
            [Concepto.Descanso] = 60,
        });
        // El descanso sigue intacto entre los intervalos de F1 (no fue removido ni partido).
        resultado.DesglosePorFranja[0].Intervalos.Should().Equal(new[]
        {
            Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna),
            Clasif(M(12), M(13), Concepto.Descanso),
            Clasif(M(13), M(17), Concepto.OrdinariaDiurna),
        });
    }
}
