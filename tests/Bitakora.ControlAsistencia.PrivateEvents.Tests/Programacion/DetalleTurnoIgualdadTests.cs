// Issue #288: Tests de contrato IEquatable para DetalleTurno.
// Dos intervenciones sobre el record por defecto:
// 1) Descripcion (dato derivado, texto de CatalogoTurnos.ToString()) EXCLUIDA de Equals/GetHashCode.
// 2) Bug latente corregido: FranjasOrdinarias es IReadOnlyList y el record por defecto la compara
//    por referencia (ADR-0015 advierte sobre esto; mismo bug que #129 ya corrigio en
//    DetalleFranjaOrdinaria). Equals/GetHashCode propios comparan FranjasOrdinarias POR VALOR
//    (SequenceEqual).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleTurnoIgualdadTests : IgualdadTestBase<DetalleTurno>
{
    private static DetalleFranjaOrdinaria FranjaOrdinaria() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)");

    protected override DetalleTurno CrearInstancia() =>
        new("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");

    protected override DetalleTurno CrearInstanciaCopia() =>
        new("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");

    protected override IEnumerable<(string, DetalleTurno)> CrearInstanciasDiferentes()
    {
        yield return ("Nombre",
            new DetalleTurno("Turno Tarde", [FranjaOrdinaria()], "Turno Tarde (06:00-14:00)"));
        yield return ("FranjasOrdinarias",
            new DetalleTurno("Turno Manana", [], "Turno Manana"));
    }

    // CA-4: dos instancias que difieren SOLO en Descripcion son iguales (dato derivado, no identidad).

    [Fact]
    public void Equals_RetornaTrue_CuandoSoloDescripcionEsDiferente()
    {
        var a = new DetalleTurno("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");
        var b = new DetalleTurno("Turno Manana", [FranjaOrdinaria()], "otro texto distinto");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoSoloDescripcionEsDiferente()
    {
        var a = new DetalleTurno("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)");
        var b = new DetalleTurno("Turno Manana", [FranjaOrdinaria()], "otro texto distinto");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // CA-5: bug latente corregido - dos DetalleTurno con franjas EQUIVALENTES en listas DISTINTAS
    // son iguales. Antes de esta correccion, el record por defecto comparaba FranjasOrdinarias por
    // referencia y este caso fallaba (mismo bug que #129 corrigio en DetalleFranjaOrdinaria).

    [Fact]
    public void Equals_RetornaTrue_CuandoFranjasSonInstanciasDeListaDiferentesConMismoContenido()
    {
        var a = new DetalleTurno("Turno Manana",
            new List<DetalleFranjaOrdinaria> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");
        var b = new DetalleTurno("Turno Manana",
            new List<DetalleFranjaOrdinaria> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoFranjasSonInstanciasDeListaDiferentesConMismoContenido()
    {
        var a = new DetalleTurno("Turno Manana",
            new List<DetalleFranjaOrdinaria> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");
        var b = new DetalleTurno("Turno Manana",
            new List<DetalleFranjaOrdinaria> { FranjaOrdinaria() }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
