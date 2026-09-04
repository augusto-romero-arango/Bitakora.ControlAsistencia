// Issue #2: Tests de igualdad por valor para FranjaOrdinaria
using AwesomeAssertions;

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class FranjaOrdinariaIgualdadTests : IgualdadTestBase<FranjaOrdinaria>
{
    protected override FranjaOrdinaria CrearInstancia() =>
        FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

    protected override FranjaOrdinaria CrearInstanciaCopia() =>
        FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

    protected override IEnumerable<(string, FranjaOrdinaria)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            FranjaOrdinaria.Crear(new TimeOnly(7, 0), new TimeOnly(12, 0)));
        yield return ("HoraFin",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        // Issue #598: 24 h exactas -- el unico par (inicio, fin) que exige el offset explicito, porque
        // 06:00-06:00 sin offset lo infiere en 0 y cae en DuracionNoPositiva. Con el tope de 24 h ya no
        // existe ningun par que difiera del base SOLO en DiaOffsetFin: sumar +1 anade 1440 min a una
        // duracion base positiva y siempre excede el tope.
        yield return ("OffsetFin",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(6, 0), diaOffsetFin: 1));
        yield return ("Descansos",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
                descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]));
        yield return ("Extras",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
                extras: [SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(7, 0))]));
        // Issue #335 CA-5: la sede prearmada es dato de identidad del diseno de la franja.
        yield return ("Sede",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
                sede: new SedeProgramada("SEDE-SUBA", "Suba")));
    }

    // Tests adicionales especificos de FranjaOrdinaria (colecciones de hijos)

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValoresConHijos()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))],
            extras: [SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0))]);
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))],
            extras: [SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0))]);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoMismosValoresConHijos()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
