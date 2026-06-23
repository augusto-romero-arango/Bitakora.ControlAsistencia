// HU-116: Consolidar DesgloseFranjas del dia con compensacion cronologica inversa.
// Familia 1 - Compensacion cross-franja basica (turno partido dia habil).
// Franja 1 (programada): 8:00-12:00. Franja 2 (programada): 14:00-18:00.
//
// Tests directos sobre ConsolidadorDesgloseHoras.Consolidar - logica pura, sin harness.
// Datos sinteticos: cada DesgloseFranja se construye a mano (ver ConsolidadorDesgloseHorasTestData).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasBasicoTests
{
    [Fact]
    public void Consolidar_CompensaTodoElRetardoConElExcedente_CuandoElExcedenteIgualaElRetardo()
    {
        // CA-1: F1 retardo 30min (entrada 8:30, salida 12:00), F2 excedente 30min (salida 18:30).
        // La compensacion cross-franja come 30min cronologico inverso = [18:00-18:30] completo.
        // RetardoNeto = 0, extras del dia = 0, ordinarias = 450min (210 + 240).
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 30), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
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
        // CA-2: F1 retardo 30min, F2 excedente 45min (salida 18:45). La compensacion come 30min
        // desde el final = [18:15-18:45]; el intervalo extra 18:00-18:45 se parte (IntervaloTemporal.Partir)
        // en [18:00-18:15] (extra genuina visible) + [18:15-18:45] (compensado). RetardoNeto = 0.
        // Extras del dia = 15min ExtraDiurna.
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

        resultado.RetardoTotal.Should().Be(Retardo(
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
        // CA-3: F1 retardo 30min, F2 puntual sin excedente. No hay extras que consumir:
        // MinutosCompensados = 0, RetardoNeto = 30. Extras del dia = 0.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            Retardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [Clasif(M(14), M(18), Concepto.OrdinariaDiurna)],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(Retardo(
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
        // CA-4: F1 puntual, F2 puntual. RetardoTotal == DetalleRetardo.Vacio, extras = 0,
        // ordinarias = 480min (240 + 240).
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8), M(12), Concepto.OrdinariaDiurna)],
            DetalleRetardo.Vacio);
        var franja2 = Desglose(
            Programada(14, 18),
            [Clasif(M(14), M(18), Concepto.OrdinariaDiurna)],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(DetalleRetardo.Vacio);
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            [Concepto.OrdinariaDiurna] = 480,
        });
    }

    [Fact]
    public void Consolidar_ConservaElExcedenteComoExtra_CuandoNoHayRetardoQueCompensar()
    {
        // CA-5: F1 puntual, F2 excedente 20min. No hay retardo que compensar => el excedente
        // sobrevive como extra. Extras del dia = 20min ExtraDiurna, RetardoTotal == Vacio.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8), M(12), Concepto.OrdinariaDiurna)],
            DetalleRetardo.Vacio);
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(18, 20), Concepto.ExtraDiurna),
            ],
            DetalleRetardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(DetalleRetardo.Vacio);
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
