// Equals/GetHashCode propios EXCLUYEN Descripcion (dato derivado, texto de CatalogoTurnos.ToString())
// y comparan FranjasOrdinarias POR VALOR (SequenceEqual): el record por defecto compararia la
// IReadOnlyList por referencia.
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
        yield return ("AmplitudVentanaEntradaEnMinutos",
            new DetalleTurno("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)", 180));
        yield return ("EsHorarioAbierto",
            new DetalleTurno("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)", 0, true));
    }

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
