// Descripcion es un dato derivado (el formato de SubFranja.ToString()): Equals/GetHashCode propios la
// EXCLUYEN, dos instancias que solo difieren en el texto formateado son la misma sub-franja.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleSubFranjaIgualdadTests : IgualdadTestBase<DetalleSubFranja>
{
    protected override DetalleSubFranja CrearInstancia() =>
        new(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");

    protected override DetalleSubFranja CrearInstanciaCopia() =>
        new(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");

    protected override IEnumerable<(string, DetalleSubFranja)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            new DetalleSubFranja(new TimeOnly(10, 5), new TimeOnly(10, 15), 0, 0, "(10:05-10:15)"));
        yield return ("HoraFin",
            new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 30), 0, 0, "(10:00-10:30)"));
        yield return ("DiaOffsetInicio",
            new DetalleSubFranja(new TimeOnly(1, 0), new TimeOnly(1, 15), 1, 0, "(01:00+1-01:15)"));
        yield return ("DiaOffsetFin",
            new DetalleSubFranja(new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)"));
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoSoloDescripcionEsDiferente()
    {
        var a = new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");
        var b = new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "otro texto distinto");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoSoloDescripcionEsDiferente()
    {
        var a = new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)");
        var b = new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "otro texto distinto");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
