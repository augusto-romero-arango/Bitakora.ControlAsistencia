// Issue #2: Tests de igualdad por valor para FranjaOrdinaria
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

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
        yield return ("OffsetFin",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0), diaOffsetFin: 1));
        yield return ("Descansos",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
                descansos: [SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]));
        yield return ("Extras",
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
                extras: [SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(7, 0))]));
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
