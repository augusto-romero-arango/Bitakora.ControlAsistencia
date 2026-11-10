// ToDetalle().Descripcion persiste tal cual el formato de FranjaOrdinaria.ToString().
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class FranjaOrdinariaToDetalleTests
{
    [Fact]
    public void ToDetalle_TieneDescripcionCoherenteConToString_CuandoFranjaSinHijos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        var detalle = franja.ToDetalle();

        detalle.Descripcion.Should().Be(franja.ToString());
    }

    [Fact]
    public void ToDetalle_TieneDescripcionCoherenteConToString_CuandoFranjaConDescansosYExtras()
    {
        var descanso = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso], extras: [extra]);

        var detalle = franja.ToDetalle();

        detalle.Descripcion.Should().Be(franja.ToString());
    }

    // Cada sub-franja hija lleva su propia Descripcion coherente con su ToString(), no solo el padre.
    [Fact]
    public void ToDetalle_PropagaDescripcionCoherenteEnHijos_CuandoFranjaConDescanso()
    {
        var descanso = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        var detalle = franja.ToDetalle();

        detalle.Descansos[0].Descripcion.Should().Be(descanso.ToString());
    }

    [Fact]
    public void ToDetalle_CopiaLaSede_CuandoFranjaTieneSedeAsignada()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0), sede: sede);

        var detalle = franja.ToDetalle();

        detalle.Sede.Should().Be(sede);
    }

    [Fact]
    public void ToDetalle_DejaSedeNull_CuandoFranjaNoTieneSedeAsignada()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var detalle = franja.ToDetalle();

        detalle.Sede.Should().BeNull();
    }
}
