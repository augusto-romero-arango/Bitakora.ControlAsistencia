using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoIgualdadTests : IgualdadTestBase<Turno>
{
    private static FranjaOrdinaria Manana() =>
        FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

    private static FranjaOrdinaria Tarde() =>
        FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

    protected override Turno CrearInstancia() => Turno.Crear("Turno Manana", false, [Manana()]);

    protected override Turno CrearInstanciaCopia() => Turno.Crear("Turno Manana", false, [Manana()]);

    protected override IEnumerable<(string, Turno)> CrearInstanciasDiferentes()
    {
        yield return ("Nombre", Turno.Crear("Turno Tarde", false, [Manana()]));
        yield return ("EsDescanso", Turno.Crear("Turno Manana", true, [Manana()]));
        yield return ("Franjas", Turno.Crear("Turno Manana", false, [Tarde()]));
        yield return ("CantidadDeFranjas", Turno.Crear("Turno Manana", false, [Manana(), Tarde()]));
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoUnaFranjaHijaDifiere()
    {
        var conDescanso = Turno.Crear("Turno Manana", false,
            [Manana().ConDescanso(new TimeOnly(10, 0), new TimeOnly(10, 30))]);

        CrearInstancia().Equals(conDescanso).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoFranjasConHijasIguales()
    {
        Turno ConDescanso() => Turno.Crear("Turno Manana", false,
            [Manana().ConDescanso(new TimeOnly(10, 0), new TimeOnly(10, 30))]);

        ConDescanso().GetHashCode().Should().Be(ConDescanso().GetHashCode());
    }
}
