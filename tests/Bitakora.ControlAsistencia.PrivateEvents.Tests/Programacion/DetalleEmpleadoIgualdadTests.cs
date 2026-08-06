// Issue #318: DetalleEmpleado es el payload propio de PrivateEvents (tres islas, MEF-ADR-0039
// decision 6) que sustituye a InformacionEmpleado (PublicEvents) en ProgramacionTurnoDiarioSolicitada.
// A diferencia de DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja, todos sus campos son
// string: la igualdad por valor del record por defecto es correcta, sin Equals/GetHashCode custom
// (no hay IReadOnlyList que el record compare por referencia, ADR-0015).

using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleEmpleadoIgualdadTests : IgualdadTestBase<DetalleEmpleado>
{
    protected override DetalleEmpleado CrearInstancia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override DetalleEmpleado CrearInstanciaCopia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override IEnumerable<(string, DetalleEmpleado)> CrearInstanciasDiferentes()
    {
        yield return ("EmpleadoId",
            new DetalleEmpleado("EMP-002", "CC", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("TipoIdentificacion",
            new DetalleEmpleado("EMP-001", "CE", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("NumeroIdentificacion",
            new DetalleEmpleado("EMP-001", "CC", "9999999999", "Luis Augusto", "Barreto"));
        yield return ("Nombres",
            new DetalleEmpleado("EMP-001", "CC", "1234567890", "Otro Nombre", "Barreto"));
        yield return ("Apellidos",
            new DetalleEmpleado("EMP-001", "CC", "1234567890", "Luis Augusto", "Otro Apellido"));
    }
}
