// Issue #319: Tests de contrato IEquatable para TurnoProgramado (record propio de
// Programacion.DomainEvents, tres islas). Equals/GetHashCode propios comparan FranjasOrdinarias
// POR VALOR (SequenceEqual) y EXCLUYEN Descripcion (dato derivado, no identidad del turno) --
// mismo criterio que DetalleTurno (issue #288).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoProgramadoIgualdadTests : IgualdadTestBase<TurnoProgramado>
{
    private static FranjaProgramada FranjaOrdinaria() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)");

    protected override TurnoProgramado CrearInstancia() =>
        new("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");

    protected override TurnoProgramado CrearInstanciaCopia() =>
        new("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");

    protected override IEnumerable<(string, TurnoProgramado)> CrearInstanciasDiferentes()
    {
        yield return ("Nombre",
            new TurnoProgramado("Turno Tarde", [FranjaOrdinaria()], "Turno Tarde (06:00-14:00)"));
        yield return ("FranjasOrdinarias",
            new TurnoProgramado("Turno Manana", [], "Turno Manana"));
    }

    // CA-1: dos instancias que difieren SOLO en Descripcion son iguales (dato derivado, no identidad).

    [Fact]
    public void Equals_RetornaTrue_CuandoSoloDescripcionEsDiferente()
    {
        var a = new TurnoProgramado("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");
        var b = new TurnoProgramado("Turno Manana", [FranjaOrdinaria()], "otro texto distinto");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoSoloDescripcionEsDiferente()
    {
        var a = new TurnoProgramado("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");
        var b = new TurnoProgramado("Turno Manana", [FranjaOrdinaria()], "otro texto distinto");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // Bug latente evitado (mismo patron que DetalleTurno, issue #129/#288): dos TurnoProgramado
    // con franjas EQUIVALENTES en listas DISTINTAS son iguales -- SequenceEqual, no referencia.

    [Fact]
    public void Equals_RetornaTrue_CuandoFranjasSonInstanciasDeListaDiferentesConMismoContenido()
    {
        var a = new TurnoProgramado("Turno Manana",
            new List<FranjaProgramada> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");
        var b = new TurnoProgramado("Turno Manana",
            new List<FranjaProgramada> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");

        a.Equals(b).Should().BeTrue();
    }
}
