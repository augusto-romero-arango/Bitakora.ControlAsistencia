// Issue #319: Tests de contrato IEquatable para SubFranjaProgramada (record propio de
// Programacion.DomainEvents, tres islas). Equals/GetHashCode propios EXCLUYEN Descripcion (dato
// derivado, no identidad de la sub-franja) -- mismo criterio que DetalleSubFranja (issue #288).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class SubFranjaProgramadaIgualdadTests : IgualdadTestBase<SubFranjaProgramada>
{
    protected override SubFranjaProgramada CrearInstancia() =>
        new(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");

    protected override SubFranjaProgramada CrearInstanciaCopia() =>
        new(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");

    protected override IEnumerable<(string, SubFranjaProgramada)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            new SubFranjaProgramada(new TimeOnly(10, 5), new TimeOnly(10, 15), 0, 0, "(10:05-10:15)"));
        yield return ("HoraFin",
            new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 30), 0, 0, "(10:00-10:30)"));
        yield return ("DiaOffsetInicio",
            new SubFranjaProgramada(new TimeOnly(1, 0), new TimeOnly(1, 15), 1, 0, "(01:00+1-01:15)"));
        yield return ("DiaOffsetFin",
            new SubFranjaProgramada(new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)"));
    }

    // CA-1: dos instancias que difieren SOLO en Descripcion son iguales (dato derivado, no identidad).

    [Fact]
    public void Equals_RetornaTrue_CuandoSoloDescripcionEsDiferente()
    {
        var a = new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");
        var b = new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "otro texto distinto");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoSoloDescripcionEsDiferente()
    {
        var a = new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");
        var b = new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "otro texto distinto");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
