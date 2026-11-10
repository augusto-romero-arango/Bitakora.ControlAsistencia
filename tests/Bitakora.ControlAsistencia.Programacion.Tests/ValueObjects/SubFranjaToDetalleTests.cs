// ToDetalle().Descripcion persiste tal cual el formato de SubFranja.ToString(): una sola
// implementacion, sin un formato nuevo para el DTO plano.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class SubFranjaToDetalleTests
{
    [Fact]
    public void ToDetalle_TieneDescripcionCoherenteConToString_CuandoSubFranjaSinOffset()
    {
        var franja = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        var detalle = franja.ToDetalle();

        detalle.Descripcion.Should().Be(franja.ToString());
    }

    [Fact]
    public void ToDetalle_TieneDescripcionCoherenteConToString_CuandoSubFranjaCruzaMedianoche()
    {
        var franja = SubFranja.Crear(new TimeOnly(23, 45), new TimeOnly(0, 15),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        var detalle = franja.ToDetalle();

        detalle.Descripcion.Should().Be(franja.ToString());
    }
}
