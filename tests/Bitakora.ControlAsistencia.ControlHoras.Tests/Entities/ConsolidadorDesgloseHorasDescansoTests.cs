// Los intervalos Descanso nunca los consume la compensacion: solo las extras compensan el retardo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using static Bitakora.ControlAsistencia.ControlHoras.Tests.Entities.ConsolidadorDesgloseHorasTestData;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ConsolidadorDesgloseHorasDescansoTests
{
    [Fact]
    public void Consolidar_NoTocaElDescanso_CuandoCompensaElRetardoConElExcedente()
    {
        var franja1 = Desglose(
            Programada(8, 17),
            [
                Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna),
                Clasif(M(12), M(13), Concepto.Descanso),
                Clasif(M(13), M(17), Concepto.OrdinariaDiurna),
            ],
            CrearRetardo(retardado: [I(M(8), M(8, 30))], compensado: []));
        var franja2 = Desglose(
            Programada(18, 20),
            [
                Clasif(M(18), M(20), Concepto.OrdinariaDiurna),
                Clasif(M(20), M(20, 30), Concepto.ExtraDiurna),
            ],
            Retardo.Vacio);

        var resultado = ConsolidadorDesgloseHoras.Consolidar([franja1, franja2], franjasAnomalas: 0);

        resultado.RetardoTotal.Should().Be(CrearRetardo(
            retardado: [I(M(8), M(8, 30))],
            compensado: [I(M(20), M(20, 30))]));
        resultado.RetardoTotal.RetardoNeto.Should().Be(0);
        resultado.TotalMinutosPorConcepto.Should().Equal(new Dictionary<Concepto, int>
        {
            // F1: 8:30-12:00 (210) + 13:00-17:00 (240) = 450; F2 ordinaria: 18:00-20:00 (120) => 570.
            [Concepto.OrdinariaDiurna] = 570,
            [Concepto.Descanso] = 60,
        });
        resultado.DesglosePorFranja[0].Intervalos.Should().Equal(new[]
        {
            Clasif(M(8, 30), M(12), Concepto.OrdinariaDiurna),
            Clasif(M(12), M(13), Concepto.Descanso),
            Clasif(M(13), M(17), Concepto.OrdinariaDiurna),
        });
    }
}
