// Issue #319: Tests de contrato IEquatable para Empleado (record propio de Programacion.DomainEvents,
// tres islas). Todos los campos son string: la igualdad por valor del record por defecto ya es
// correcta, sin Equals/GetHashCode custom -- mismo criterio que DetalleEmpleado (issue #318).

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class EmpleadoIgualdadTests : IgualdadTestBase<Empleado>
{
    protected override Empleado CrearInstancia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override Empleado CrearInstanciaCopia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override IEnumerable<(string, Empleado)> CrearInstanciasDiferentes()
    {
        yield return ("EmpleadoId",
            new Empleado("EMP-002", "CC", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("TipoIdentificacion",
            new Empleado("EMP-001", "CE", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("NumeroIdentificacion",
            new Empleado("EMP-001", "CC", "9999999999", "Luis Augusto", "Barreto"));
        yield return ("Nombres",
            new Empleado("EMP-001", "CC", "1234567890", "Otro Nombre", "Barreto"));
        yield return ("Apellidos",
            new Empleado("EMP-001", "CC", "1234567890", "Luis Augusto", "Otro Apellido"));
    }
}
