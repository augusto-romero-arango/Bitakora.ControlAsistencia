// La compensacion consume las extras programadas igual que un excedente, sin distincion de origen ni
// de concepto.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasExtrasProgramadasTests
{
    [Fact]
    public void Consolidar_ConsumeUnaExtraProgramada_CuandoNoHayExcedentePorSalida()
    {
        // Consume 30 min desde el final de la extra programada: [18:30-19:00].
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
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
        // Consume 30 min de la ExtraNocturna desde el final, [19:30-20:00]; la ExtraDiurna queda intacta.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
                Clasif(M(19), M(20), Concepto.ExtraNocturna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
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
        // En domingo: consume la ExtraNocturnaDominicalFestiva y deja intactas las ordinarias dominicales.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.DominicalFestivaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.DominicalFestivaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurnaDominicalFestiva),
                Clasif(M(19), M(20), Concepto.ExtraNocturnaDominicalFestiva),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
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
