// Issue #319 CA-1: guardrail de la duplicacion deliberada de payload por rol (tres islas,
// MEF-ADR-0039 decision 2 y 6): Empleado (Programacion.DomainEvents) e InformacionEmpleado
// (PublicEvents) declaran el mismo dato en dos ensamblados que no se referencian entre si.
// Sin este guardrail, agregar un campo a uno de los dos no rompe nada y el dato se pierde en
// silencio al construir el evento persistido ProgramacionTurnoSolicitada -- mismo modo de fallo
// que DetalleEmpleadoParidadConInformacionEmpleadoTests (issue #318) ya cubre para el payload de
// bus.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class EmpleadoParidadConInformacionEmpleadoTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void Empleado_DeclaraLosMismosCamposQueInformacionEmpleado()
    {
        var campos = Campos<Empleado>();

        campos.Should().Equal(Campos<InformacionEmpleado>());
    }
}
