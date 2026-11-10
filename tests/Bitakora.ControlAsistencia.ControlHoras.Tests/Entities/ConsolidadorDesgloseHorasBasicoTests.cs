// Turno partido en dia habil: F1 programada 08:00-12:00 y F2 14:00-18:00.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasBasicoTests
{
    [Fact]
    public void Consolidar_CompensaTodoElRetardoConElExcedente_CuandoElExcedenteIgualaElRetardo()
    {
        // La compensacion cross-franja consume 30 min en orden cronologico inverso: [18:00-18:30].
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 30), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

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

    [Fact]
    public void Consolidar_DejaExtrasGenuinasYParteElIntervalo_CuandoElExcedenteSuperaElRetardo()
    {
        // La compensacion consume [18:15-18:45] desde el final y parte la extra 18:00-18:45.
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

        resultado.RetardoTotal.Should().Be(CrearRetardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(18, 15), M(18, 45))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
            [Concepto.ExtraDiurna] = 15,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
            Clasif(M(18), M(18, 15), Concepto.ExtraDiurna),
        });
    }

    [Fact]
    public void Consolidar_DejaRetardoNetoSinCompensar_CuandoNoHayExcedenteQueConsumir()
    {
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [Clasif(M(14), M(18), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: []));
        resultado.RetardoTotal.RetardoNeto.Should().Be(30);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 450,
        });
    }

    [Fact]
    public void Consolidar_NoRegistraRetardoNiExtras_CuandoTodasLasFranjasSonPuntuales()
    {
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8), M(12), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);
        var franja2 = Desglose(
            Programada(14, 18),
            [Clasif(M(14), M(18), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo.Vacio);
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 480,
        });
    }

    [Fact]
    public void Consolidar_ConservaElExcedenteComoExtra_CuandoNoHayRetardoQueCompensar()
    {
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8), M(12), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 20), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo.Vacio);
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 480,
            [Concepto.ExtraDiurna] = 20,
        });
        resultado.DesglosePorFranja[1].Intervalos.Should().Equal(new[]
        {
            Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
            Clasif(M(18), M(18, 20), Concepto.ExtraDiurna),
        });
    }
}
