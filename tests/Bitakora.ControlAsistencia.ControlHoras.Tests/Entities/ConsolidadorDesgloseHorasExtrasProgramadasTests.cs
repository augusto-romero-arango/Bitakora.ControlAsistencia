// HU-116: Consolidar DesgloseFranjas del dia con compensacion cronologica inversa.
// Familia 4 - Compensacion sobre extras programadas (no por excedente de salida). La regla aplica
// sobre TODAS las extras sin distincion de origen: el consolidador come la extra programada igual
// que comeria un excedente, y es agnostico al concepto especifico (diurna/nocturna/dominical).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasExtrasProgramadasTests
{
    [Fact]
    public void Consolidar_ConsumeUnaExtraProgramada_CuandoNoHayExcedentePorSalida()
    {
        // CA-11: F1 retardo 30min, F2 con extra programada 18:00-19:00 (ExtraDiurna 60min) y salida
        // 19:00 (sin excedente adicional). La compensacion cronologico inverso come 30min desde el
        // final de la extra = [18:30-19:00]. Resultado: 30min ExtraDiurna (18:00-18:30). Verifica que
        // las extras programadas son consumibles por compensacion sin distincion de origen.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(18, 30), M(19))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 30,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
            Clasif(M(18), M(18, 30), Concepto.ExtraDiurna),
        });
    }

    [Fact]
    public void Consolidar_ComeSoloLaExtraNocturnaProgramada_CuandoLaExtraCruzaLaFronteraNocturna()
    {
        // CA-12: F1 retardo 30min, F2 con extra programada 18:00-20:00 repartida en [ExtraDiurna
        // 18:00-19:00 (60min) + ExtraNocturna 19:00-20:00 (60min)], salida 20:00. La compensacion
        // cronologico inverso come 30min de ExtraNocturna desde el final = [19:30-20:00]. Resultado:
        // ExtraDiurna 60min intacta + ExtraNocturna 30min.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
                Clasif(M(19), M(20), Concepto.ExtraNocturna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(19, 30), M(20))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 60,
            [Concepto.ExtraNocturna] = 30,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
            Clasif(M(18), M(19), Concepto.ExtraDiurna),
            Clasif(M(19), M(19, 30), Concepto.ExtraNocturna),
        });
    }

    [Fact]
    public void Consolidar_ComeLaExtraDominicalFestiva_CuandoElDiaEsDomingo()
    {
        // CA-13: mismo escenario del CA-12 pero en domingo. Los conceptos de extras son
        // ExtraDiurnaDominicalFestiva + ExtraNocturnaDominicalFestiva (por #135), y el tiempo ordinario
        // es DominicalFestivaDiurna. La compensacion come 30min de ExtraNocturnaDominicalFestiva desde
        // el final. Verifica que la compensacion es agnostica al concepto: consume cualquier extra y
        // deja intactas las ordinarias dominicales.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.DominicalFestivaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.DominicalFestivaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurnaDominicalFestiva),
                Clasif(M(19), M(20), Concepto.ExtraNocturnaDominicalFestiva),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(19, 30), M(20))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.DominicalFestivaDiurna] = 450,
            [Concepto.ExtraDiurnaDominicalFestiva] = 60,
            [Concepto.ExtraNocturnaDominicalFestiva] = 30,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.DominicalFestivaDiurna),
            Clasif(M(18), M(19), Concepto.ExtraDiurnaDominicalFestiva),
            Clasif(M(19), M(19, 30), Concepto.ExtraNocturnaDominicalFestiva),
        });
    }
}
