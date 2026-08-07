// Issue #319: Tests de contrato IEquatable para FranjaProgramada (record propio de
// Programacion.DomainEvents, tres islas). Equals/GetHashCode propios comparan Descansos/Extras
// POR VALOR (SequenceEqual, el record por defecto compararia por referencia -- ADR-0015) y
// EXCLUYEN Descripcion (dato derivado, no identidad de la franja) -- mismo criterio que
// DetalleFranjaOrdinaria (issues #129 y #288).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

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
        // Issue #335: Sede entra en Equals/GetHashCode -- es dato de identidad del diseno de la
        // franja, a diferencia de Descripcion (derivado, excluido).
        yield return ("Sede",
            new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "",
                new SedeProgramada("SEDE-01", "Sede Principal")));
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

    // CA-1: dos instancias que difieren SOLO en Descripcion son iguales (dato derivado, no identidad).

    [Fact]
    public void Equals_RetornaTrue_CuandoSoloDescripcionEsDiferente()
    {
        var a = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "texto 1");
        var b = new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "texto 2");

        a.Equals(b).Should().BeTrue();
    }
}
