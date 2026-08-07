// Issue #288 CA-3: coherencia entre FranjaOrdinaria.ToDetalle().Descripcion y FranjaOrdinaria.ToString().
// El formato tecnico ya existente (CA-20/CA-21 de FranjaOrdinariaTests, que NO se modifica -- CA-6
// del issue) se persiste tal cual en el DTO plano.
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

    // CA-3: cada sub-franja hija tambien lleva su propia Descripcion coherente con su propio
    // ToString(), no solo el nivel padre (el issue pide Descripcion en los tres niveles).
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

    // ---------- Issue #335 CA-1/CA-2: la sede prearmada fluye (o no) al DTO plano ----------

    [Fact]
    public void ToDetalle_CopiaLaSede_CuandoFranjaTieneSedeAsignada()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0), sede: sede);

        var detalle = franja.ToDetalle();

        detalle.Sede.Should().Be(sede);
    }

    // CA-2: regresion -- una franja sin sede prearmada conserva el comportamiento actual.
    [Fact]
    public void ToDetalle_DejaSedeNull_CuandoFranjaNoTieneSedeAsignada()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var detalle = franja.ToDetalle();

        detalle.Sede.Should().BeNull();
    }
}
