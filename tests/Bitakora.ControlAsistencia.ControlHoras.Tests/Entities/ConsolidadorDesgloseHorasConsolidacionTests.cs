// HU-116: Consolidar DesgloseFranjas del dia con compensacion cronologica inversa.
// Familia 3 - Consolidacion de totales y contadores (CA-8, CA-9, CA-10) y las vigilancias de
// review (#2 suma de MinutosCompensados intra + cross-franja, #3 orden explicito de los extras).

using System.Linq;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasConsolidacionTests
{
    [Fact]
    public void Consolidar_SumaMinutosPorConceptoSobreLasFranjasAjustadas_CuandoLaCompensacionRecortaUnaExtra()
    {
        // CA-8: TotalMinutosPorConcepto es la suma de MinutosPorConcepto de cada franja DESPUES de
        // aplicar el ajuste cross-franja. F1 retardo 30min; F2 extra 18:00-18:45 (45min). La
        // compensacion recorta la extra a 18:00-18:15 (15min): el total refleja 15min, no los 45
        // del insumo. Se verifica ademas que el total iguala la suma elemento a elemento de las
        // franjas ya ajustadas (no de las de entrada).
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 45), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

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
        // CA-9: FranjasAnomalas del resultado es igual al parametro recibido (el aggregate lo cuenta
        // desde sus ControlesDeFranja en #139; aqui llega como parametro explicito).
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8), M(12), Concepto.OrdinariaDiurna)],
            DetalleRetardo.Vacio);
        var franja2 = Desglose(
            Programada(14, 18),
            [Clasif(M(14), M(18), Concepto.OrdinariaDiurna)],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 2);

        resultado.FranjasAnomalas.Should().Be(2);
        resultado.DesglosePorFranja.Should().HaveCount(2);
        resultado.RetardoTotal.Should().Be(DetalleRetardo.Vacio);
    }

    [Fact]
    public void Consolidar_RetornaDesgloseVacioConContador_CuandoNoHayFranjasCompletas()
    {
        // CA-10: Consolidar([], 3) (sin franjas completas, 3 anomalas) => DesgloseHoras vacio pero
        // con FranjasAnomalas = 3. Todos los totales en cero y RetardoTotal == DetalleRetardo.Vacio.
        var resultado = ConsolidadorDesgloseHoras.Consolidar([], franjasAnomalas: 3);

        resultado.FranjasAnomalas.Should().Be(3);
        resultado.DesglosePorFranja.Should().BeEmpty();
        resultado.RetardoTotal.Should().Be(DetalleRetardo.Vacio);
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Consolidar_SumaMinutosCompensadosIntraYCrossFranja_CuandoAmbasFuentesContribuyen()
    {
        // Vigilancia #1 + #2: una franja de entrada llega con TiempoCompensado NO vacio (estado
        // natural post-#136: ya hubo compensacion intra-franja) y ademas hay compensacion cross-franja.
        // F1 retardo 40min con excedente propio 10min ya compensado intra-franja (12:00-12:10);
        // neto remanente 30min. F2 excedente 30min (18:00-18:30). El RetardoTotal debe sumar los
        // MinutosCompensados intra (10) MAS los cross-franja (30) = 40 => RetardoNeto = 0.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 40), M(12), Concepto.OrdinariaDiurna)],
            Retardo(
                retardado: [I(M(8), M(8, 40))],
                compensado: [I(M(12), M(12, 10))]));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 30), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        // Retardado total = 40min (F1). Compensado total = 10min intra (F1) + 30min cross ([18:00-18:30]).
        resultado.RetardoTotal.Should().Be(Retardo(
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
        // Vigilancia #3: la recoleccion de extras debe ordenarse cronologicamente de forma EXPLICITA,
        // sin asumir que la lista de franjas llega ordenada cross-franja. Franjas pasadas en desorden
        // [manana, tarde-tardia, tarde-temprana]; el retardo (30min, en la manana) debe consumir el
        // excedente cronologicamente ultimo (20:00-20:30, en la franja tarde-tardia), NO el ultimo de
        // la lista de entrada (16:00-16:30). Si la ordenacion fuera implicita por orden de lista, se
        // comeria el excedente equivocado.
        var manana = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var tardeTardia = Desglose(
            Programada(18, 20),
            [
                Clasif(M(18), M(20), Concepto.OrdinariaDiurna),
                Clasif(M(20), M(20, 30), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);
        var tardeTemprana = Desglose(
            Programada(14, 16),
            [
                Clasif(M(14), M(16), Concepto.OrdinariaDiurna),
                Clasif(M(16), M(16, 30), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar(
            [manana, tardeTardia, tardeTemprana], franjasAnomalas: 0);

        // La compensacion comio el excedente cronologicamente ultimo (20:00-20:30).
        resultado.RetardoTotal.Should().Be(Retardo(
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
}
