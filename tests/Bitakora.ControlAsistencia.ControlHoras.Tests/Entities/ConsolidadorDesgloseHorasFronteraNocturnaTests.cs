// Familia 2 - Compensacion cruzando la frontera nocturna (21:00 segun #135).
// F1 08:00-12:00 y F2 14:00-18:00 con salida prolongada hasta 19:30: el excedente se reparte en
// ExtraDiurna + ExtraNocturna.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasFronteraNocturnaTests
{
    [Fact]
    public void Consolidar_ConsumeLaExtraNocturnaCompleta_CuandoElRetardoIgualaElTramoNocturno()
    {
        // Los 30 min compensados son la ExtraNocturna completa: desaparece porque solo existia por el retardo.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
                Clasif(M(19), M(19, 30), Concepto.ExtraNocturna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
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
        // Compensa 80 min: la ExtraNocturna completa + 50 min de la ExtraDiurna desde el final (la parte en
        // 18:10). El TiempoCompensado queda en orden ascendente.
        var franja1 = Desglose(
            Programada(8, 12),
            [Clasif(M(9, 20), M(12), Concepto.OrdinariaDiurna)],
            CrearRetardo(retardado: [I(M(8), M(9, 20))], compensado: []));
        var franja2 = Desglose(
            Programada(14, 18),
            [
                Clasif(M(14), M(18), Concepto.OrdinariaDiurna),
                Clasif(M(18), M(19), Concepto.ExtraDiurna),
                Clasif(M(19), M(19, 30), Concepto.ExtraNocturna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
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
