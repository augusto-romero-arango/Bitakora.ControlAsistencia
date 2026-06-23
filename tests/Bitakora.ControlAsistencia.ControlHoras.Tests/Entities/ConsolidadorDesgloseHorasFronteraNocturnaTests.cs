// HU-116: Consolidar DesgloseFranjas del dia con compensacion cronologica inversa.
// Familia 2 - Compensacion cruzando la frontera nocturna (21:00 segun #135).
// Franja 1 (programada): 8:00-12:00. Franja 2 (programada): 14:00-18:00, salida prolongada
// hasta 19:30 cruzando la banda nocturna (excedente repartido en ExtraDiurna + ExtraNocturna).
//
// Datos sinteticos: los conceptos diurno/nocturno se fijan a mano para focalizar el test en
// la compensacion cronologica inversa (la clasificacion por banda ya esta cubierta en #135).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasFronteraNocturnaTests
{
    [Fact]
    public void Consolidar_ConsumeLaExtraNocturnaCompleta_CuandoElRetardoIgualaElTramoNocturno()
    {
        // CA-6: F1 retardo 30min, F2 excedente 90min repartido en [ExtraDiurna 18:00-19:00 (60min)
        // + ExtraNocturna 19:00-19:30 (30min)]. La compensacion cronologico inverso come 30min =
        // los 30min ExtraNocturna completos. Resultado: 60min ExtraDiurna + 0min ExtraNocturna.
        // RetardoNeto = 0; la ExtraNocturna desaparece porque solo existia por el retardo.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
                Clasif(M(19), M(19, 30), Concepto.ExtraNocturna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(19), M(19, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 60,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
            Clasif(M(18), M(19), Concepto.ExtraDiurna),
        });
    }

    [Fact]
    public void Consolidar_ConsumeLaNocturnaYParteLaDiurna_CuandoElRetardoSuperaElTramoNocturno()
    {
        // CA-7: F1 retardo 80min (entrada 9:20), F2 excedente 90min como en CA-6. La compensacion
        // cronologico inverso come 30min ExtraNocturna completos + 50min ExtraDiurna desde el final
        // (18:10-19:00). Resultado: 10min ExtraDiurna (18:00-18:10). RetardoNeto = 0.
        // Ejerce IntervaloTemporal.Partir sobre la ExtraDiurna 18:00-19:00. El TiempoCompensado
        // cross-franja queda en orden cronologico ascendente [18:10-19:00, 19:00-19:30].
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(9, 20), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(9, 20))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
                Clasif(M(19), M(19, 30), Concepto.ExtraNocturna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
            retardado: [I(M(8), M(9, 20))],
            compensado: [I(M(18, 10), M(19)), I(M(19), M(19, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            // F1: 9:20-12:00 = 160min. F2 ordinaria: 14:00-18:00 = 240min.
            [Concepto.OrdinariaDiurna] = 400,
            [Concepto.ExtraDiurna] = 10,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
            Clasif(M(18), M(18, 10), Concepto.ExtraDiurna),
        });
    }
}
