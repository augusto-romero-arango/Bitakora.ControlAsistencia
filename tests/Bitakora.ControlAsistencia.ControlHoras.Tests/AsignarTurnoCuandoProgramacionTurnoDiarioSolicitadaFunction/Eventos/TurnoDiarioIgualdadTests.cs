// Issue #322: paridad de campos e igualdad de TurnoDiario (ControlHoras.DomainEvents) con
// DetalleTurno (PrivateEvents.Programacion) -- payload por rol, CA-ADR-0029 decision #5.
// CA-1: TurnoDiario replica la semantica de igualdad del original: FranjasOrdinarias
// (IReadOnlyList) se compara POR VALOR (SequenceEqual), no por referencia (MEF-ADR-0012), y
// Descripcion (dato derivado) queda EXCLUIDA de la identidad del turno. Mismo patron que
// DetalleTurnoIgualdadTests (PrivateEvents.Tests), del cual este archivo es espejo deliberado.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class TurnoDiarioIgualdadTests : IgualdadTestBase<TurnoDiario>
{
    private static FranjaProgramada FranjaOrdinaria() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)");

    protected override TurnoDiario CrearInstancia() =>
        new("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");

    protected override TurnoDiario CrearInstanciaCopia() =>
        new("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");

    protected override IEnumerable<(string, TurnoDiario)> CrearInstanciasDiferentes()
    {
        yield return ("Nombre",
            new TurnoDiario("Turno Tarde", [FranjaOrdinaria()], "Turno Tarde (06:00-14:00)"));
        yield return ("FranjasOrdinarias",
            new TurnoDiario("Turno Manana", [], "Turno Manana"));
    }

    // CA-1: dos instancias que difieren SOLO en Descripcion son iguales (dato derivado, no identidad).

    [Fact]
    public void Equals_RetornaTrue_CuandoSoloDescripcionEsDiferente()
    {
        var a = new TurnoDiario("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");
        var b = new TurnoDiario("Turno Manana", [FranjaOrdinaria()], "otro texto distinto");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoSoloDescripcionEsDiferente()
    {
        var a = new TurnoDiario("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");
        var b = new TurnoDiario("Turno Manana", [FranjaOrdinaria()], "otro texto distinto");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // CA-1: bug latente evitado - dos TurnoDiario con franjas EQUIVALENTES en listas DISTINTAS
    // son iguales (el record por defecto compararia FranjasOrdinarias por referencia).

    [Fact]
    public void Equals_RetornaTrue_CuandoFranjasSonInstanciasDeListaDiferentesConMismoContenido()
    {
        var a = new TurnoDiario("Turno Manana",
            new List<FranjaProgramada> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");
        var b = new TurnoDiario("Turno Manana",
            new List<FranjaProgramada> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoFranjasSonInstanciasDeListaDiferentesConMismoContenido()
    {
        var a = new TurnoDiario("Turno Manana",
            new List<FranjaProgramada> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");
        var b = new TurnoDiario("Turno Manana",
            new List<FranjaProgramada> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
