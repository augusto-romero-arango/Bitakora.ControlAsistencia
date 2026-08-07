// Issue #336: guardrail de la duplicacion deliberada de payload por rol (tres islas,
// CA-ADR-0029 decisiones #2 y #5): SedeProgramada (ControlHoras.DomainEvents) y DetalleSede
// (PrivateEvents.Programacion) declaran el mismo dato en dos ensamblados que no se referencian
// entre si. Sin este guardrail, agregar un campo a uno de los dos no rompe nada y el dato se
// pierde en silencio en MapearFranja -- el mismo modo de fallo que ya cubren los pares de gemelos
// analogos de este ensamblado (Empleado/DetalleEmpleado, TurnoDiario/DetalleTurno,
// FranjaProgramada/DetalleFranjaOrdinaria, SubFranjaProgramada/DetalleSubFranja) y el gemelo
// equivalente del lado de Programacion (SedeProgramadaParidadConDetalleSedeTests, issue #331).

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

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
