// Issue #288 CA-3: coherencia entre SubFranja.ToDetalle().Descripcion y SubFranja.ToString().
// El formato tecnico ya existente (CA-20/CA-21 de SubFranjaTests, que NO se modifica -- CA-6 del
// issue) se persiste tal cual en el DTO plano: una sola implementacion, sin inventar un formato
// nuevo (decision del issue). Hoy SubFranja.ToDetalle() todavia asigna un placeholder (fase roja);
// este test queda en rojo hasta que el implementer asigne ToString() en el sitio de produccion.
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
