// Issue #322: paridad de campos e igualdad de Empleado (ControlHoras.DomainEvents) con
// InformacionEmpleado (PublicEvents.Empleados) y DetalleEmpleado (PrivateEvents.Programacion)
// -- payload por rol, CA-ADR-0029 decision #5.
// CA-1: todos los campos son string, asi que la igualdad por valor del record por defecto ya es
// correcta y no lleva Equals/GetHashCode custom. Este test congela ese contrato: el dia que alguien
// agregue una coleccion al record (o un Equals custom incompleto), la igualdad deja de cumplirse
// aqui. Espejo de EmpleadoIgualdadTests (Programacion.Tests, issue #319).

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

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
