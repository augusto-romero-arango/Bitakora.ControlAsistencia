// Espejo de DetalleTurnoIgualdadTests (PrivateEvents.Tests): FranjasOrdinarias se compara por valor y
// Descripcion (dato derivado) queda fuera de la identidad.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

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
        yield return ("AmplitudVentanaEntradaEnMinutos",
            new TurnoDiario("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)", 180));
        yield return ("EsHorarioAbierto",
            new TurnoDiario("Turno Manana", [FranjaOrdinaria()], "Turno Manana (06:00-14:00)", 0, true));
        yield return ("FranjasOrdinarias",
            new TurnoDiario("Turno Manana", [], "Turno Manana"));
    }

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

    // El record por defecto compararia FranjasOrdinarias por referencia.
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
