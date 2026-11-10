using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasConsolidacionTests
{
    [Fact]
    public void Consolidar_SumaMinutosPorConceptoSobreLasFranjasAjustadas_CuandoLaCompensacionRecortaUnaExtra()
    {
        // El total se calcula sobre las franjas ya ajustadas por la compensacion cross-franja: refleja
        // 15 min de extra, no los 45 del insumo.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 45), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        var sumaDeFranjasAjustadas = resultado.DesglosePorFranja
            .SelectMany(f => f.MinutosPorConcepto)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(sumaDeFranjasAjustadas);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 15,
        });
    }

    [Fact]
    public void Consolidar_PropagaFranjasAnomalas_CuandoSeRecibeElParametro()
    {
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8), M(12), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);
        var franja2 = Desglose(
            Programada(14, 18),
            [Clasif(M(14), M(18), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 2);

        resultado.FranjasAnomalas.Should().Be(2);
        resultado.DesglosePorFranja.Should().HaveCount(2);
        resultado.RetardoTotal.Should().Be(Retardo.Vacio);
    }

    [Fact]
    public void Consolidar_RetornaDesgloseVacioConContador_CuandoNoHayFranjasCompletas()
    {
        var resultado = ConsolidadorDesgloseHoras.Consolidar([], franjasAnomalas: 3);

        resultado.FranjasAnomalas.Should().Be(3);
        resultado.DesglosePorFranja.Should().BeEmpty();
        resultado.RetardoTotal.Should().Be(Retardo.Vacio);
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Consolidar_SumaMinutosCompensadosIntraYCrossFranja_CuandoAmbasFuentesContribuyen()
    {
        // F1 llega con compensacion intra-franja previa (12:00-12:10) y ademas hay cross-franja: el
        // compensado total suma ambas (10 + 30 = 40).
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 40), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(
                retardado: [I(M(8), M(8, 40))],
                compensado: [I(M(12), M(12, 10))]));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 30), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
            retardado: [I(M(8), M(8, 40))],
            compensado: [I(M(12), M(12, 10)), I(M(18), M(18, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            // F1: 8:40-12:00 = 200min. F2 ordinaria: 14:00-18:00 = 240min. Extra consumida por completo.
            [Concepto.OrdinariaDiurna] = 440,
        });
    }

    [Fact]
    public void Consolidar_ConsumeElExcedenteCronologicamenteUltimo_CuandoLasFranjasLleganEnDesorden()
    {
        // Franjas en desorden a proposito: el retardo debe consumir el excedente cronologicamente ultimo
        // (20:00-20:30), no el ultimo de la lista (16:00-16:30).
        var manana = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var tardeTardia = Desglose(
            Programada(18, 20),
            [
                Clasif(M(18), M(20), Concepto.OrdinariaDiurna),
                Clasif(M(20), M(20, 30), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);
        var tardeTemprana = Desglose(
            Programada(14, 16),
            [
                Clasif(M(14), M(16), Concepto.OrdinariaDiurna),
                Clasif(M(16), M(16, 30), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar(
            [manana, tardeTardia, tardeTemprana], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(20), M(20, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        // tardeTardia (indice 1) perdio su excedente; tardeTemprana (indice 2) lo conserva.
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(18), M(20), Concepto.OrdinariaDiurna),
        });
        resultado.DesglosePorFranja[2].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(16), Concepto.OrdinariaDiurna),
            Clasif(M(16), M(16, 30), Concepto.ExtraDiurna),
        });
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 30,
        });
    }

    [Fact]
    public void Consolidar_NoConsumeExtrasNiCastiga_CuandoElRetardoEsSinCastigo()
    {
        // En un turno de horario abierto el retardo de una franja (30 min) no consume la extra de otra y
        // el neto del dia queda en 0.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 45), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar(
            [franja1, franja2], franjasAnomalas: 0, sinCastigoPorRetardo: true);

        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 45,
        });
    }
}
