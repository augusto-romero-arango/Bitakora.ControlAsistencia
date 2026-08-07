// Issue #129: Tests de contrato IEquatable para DetalleFranjaOrdinaria.
// DetalleFranjaOrdinaria es record con override manual de Equals/GetHashCode que compara
// las colecciones Descansos y Extras con SequenceEqual (en lugar de la igualdad por
// referencia que el record genera por defecto - ADR-0015).
// Issue #288: se agrego Descripcion (dato derivado, texto del formato tecnico) al constructor
// primario. NO participa en Equals/GetHashCode (ver DetalleFranjaOrdinaria.cs); los construction
// sites de este archivo pasan "" porque el valor es irrelevante para estos tests de igualdad.
// Issue #341: se agrego Sede (dato de IDENTIDAD, la sede efectiva ya resuelta por la cascada) --
// SI participa en Equals/GetHashCode, a diferencia de Descripcion.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleFranjaOrdinariaIgualdadTests : IgualdadTestBase<DetalleFranjaOrdinaria>
{
    private static DetalleSubFranja Descanso() =>
        new(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "");

    private static DetalleSubFranja Extra() =>
        new(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "");

    private static DetalleSede Sede() => new("SEDE-01", "Sede Principal");

    protected override DetalleFranjaOrdinaria CrearInstancia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "", Sede());

    protected override DetalleFranjaOrdinaria CrearInstanciaCopia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "", Sede());

    protected override IEnumerable<(string, DetalleFranjaOrdinaria)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            new DetalleFranjaOrdinaria(new TimeOnly(7, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "", Sede()));
        yield return ("HoraFin",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(18, 0), 0, [Descanso()], [Extra()], "", Sede()));
        yield return ("DiaOffsetFin",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 1, [Descanso()], [Extra()], "", Sede()));
        yield return ("Descansos",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], [Extra()], "", Sede()));
        yield return ("Extras",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [], "", Sede()));
        yield return ("Sede",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "", Sede: null));
    }

    // Cobertura especifica del override: las listas se comparan por valor, no por referencia.

    [Fact]
    public void Equals_RetornaTrue_CuandoListasSonInstanciasDiferentesConMismoContenido()
    {
        var a = new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new DetalleSubFranja(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [new DetalleSubFranja(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "")], "");
        var b = new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new DetalleSubFranja(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [new DetalleSubFranja(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "")], "");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoListasSonInstanciasDiferentesConMismoContenido()
    {
        var a = new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new DetalleSubFranja(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [], "");
        var b = new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new DetalleSubFranja(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
            [], "");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
