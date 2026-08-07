// Issue #331: guardrail de la duplicacion deliberada de payload por rol (tres islas,
// CA-ADR-0029 decisiones #2 y #5): SedeProgramada (Programacion.DomainEvents) y DetalleSede
// (PrivateEvents) declaran el mismo dato en dos ensamblados que no se referencian entre si.
// Sin este guardrail, agregar un campo a uno de los dos no rompe nada y el dato se pierde en
// silencio en MapearSede -- el mismo modo de fallo que ya cubren los cuatro pares de gemelos
// anteriores (Empleado/InformacionEmpleado #319, TurnoProgramado/DetalleTurno,
// FranjaProgramada/DetalleFranjaOrdinaria, SubFranjaProgramada/DetalleSubFranja).

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class SedeProgramadaParidadConDetalleSedeTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void SedeProgramada_DeclaraLosMismosCamposQueDetalleSede()
    {
        var campos = Campos<SedeProgramada>();

        campos.Should().Equal(Campos<DetalleSede>());
    }
}
