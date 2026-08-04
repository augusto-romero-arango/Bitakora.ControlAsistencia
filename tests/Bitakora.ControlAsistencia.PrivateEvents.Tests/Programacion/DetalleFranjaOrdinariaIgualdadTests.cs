// Issue #129: Tests de contrato IEquatable para DetalleFranjaOrdinaria.
// DetalleFranjaOrdinaria es record con override manual de Equals/GetHashCode que compara
// las colecciones Descansos y Extras con SequenceEqual (en lugar de la igualdad por
// referencia que el record genera por defecto - ADR-0015).
// Issue #288: se agrego Descripcion (dato derivado, texto del formato tecnico) al constructor
// primario. NO participa en Equals/GetHashCode (ver DetalleFranjaOrdinaria.cs); los construction
// sites de este archivo pasan "" porque el valor es irrelevante para estos tests de igualdad.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleFranjaOrdinariaIgualdadTests : IgualdadTestBase<DetalleFranjaOrdinaria>
{
    private static DetalleSubFranja Descanso() =>
        new(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "");

    private static DetalleSubFranja Extra() =>
        new(new TimeOnly(17, 0), new TimeOnly(19, 0), 0, 0, "");

    protected override DetalleFranjaOrdinaria CrearInstancia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "");

    protected override DetalleFranjaOrdinaria CrearInstanciaCopia() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], "");

    protected override IEnumerable<(string, DetalleFranjaOrdinaria)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            new DetalleFranjaOrdinaria(new TimeOnly(7, 0), new TimeOnly(17, 0), 0, [Descanso()], [Extra()], ""));
        yield return ("HoraFin",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(18, 0), 0, [Descanso()], [Extra()], ""));
        yield return ("DiaOffsetFin",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 1, [Descanso()], [Extra()], ""));
        yield return ("Descansos",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], [Extra()], ""));
        yield return ("Extras",
            new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [Descanso()], [], ""));
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
