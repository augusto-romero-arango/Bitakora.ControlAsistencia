// Issue #322: paridad de campos e igualdad de FranjaProgramada (ControlHoras.DomainEvents) con
// DetalleFranjaOrdinaria (PrivateEvents.Programacion) -- payload por rol, CA-ADR-0029 decision #5.
// CA-1: FranjaProgramada replica la semantica de igualdad del original: Descansos/Extras
// (IReadOnlyList) se comparan POR VALOR (SequenceEqual), no por referencia (MEF-ADR-0012). Mismo
// patron que DetalleFranjaOrdinariaIgualdadTests (PrivateEvents.Tests), del cual este archivo es
// espejo deliberado.
//
// Issue #336: Sede se agrega al caso "instancias diferentes" -- a diferencia de Descripcion, SI
// entra en la identidad de la franja (Equals/GetHashCode custom).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class FranjaProgramadaIgualdadTests : IgualdadTestBase<FranjaProgramada>
{
    private static SubFranjaProgramada Descanso() =>
        new(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "");

    private static SubFranjaProgramada Extra() =>
        new(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "");

    protected override FranjaProgramada CrearInstancia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "");

    protected override FranjaProgramada CrearInstanciaCopia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "");

    protected override IEnumerable<(string, FranjaProgramada)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            new FranjaProgramada(new TimeOnly(7, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], ""));
        yield return ("HoraFin",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(18, 0), 0, [Descanso()], [Extra()], ""));
        yield return ("DiaOffsetFin",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 1, [Descanso()], [Extra()], ""));
        yield return ("Descansos",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], [Extra()], ""));
        yield return ("Extras",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [], ""));
        yield return ("Sede",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "",
                new SedeProgramada("SEDE-SUBA", "Suba")));
    }

    // Cobertura especifica del override: las listas se comparan por valor, no por referencia.

    [Fact]
    public void Equals_RetornaTrue_CuandoListasSonInstanciasDiferentesConMismoContenido()
    {
        var a = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [new SubFranjaProgramada(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "")], "");
        var b = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [new SubFranjaProgramada(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "")], "");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoListasSonInstanciasDiferentesConMismoContenido()
    {
        var a = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [], "");
        var b = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [], "");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
