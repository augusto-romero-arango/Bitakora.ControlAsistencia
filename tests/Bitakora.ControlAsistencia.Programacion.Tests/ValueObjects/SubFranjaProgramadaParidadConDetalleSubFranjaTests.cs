// Guardrail de la duplicacion deliberada de payload por rol (tres islas, MEF-ADR-0039):
// SubFranjaProgramada (Programacion.DomainEvents) y DetalleSubFranja (PrivateEvents) declaran el mismo
// dato en dos ensamblados que no se referencian entre si. Sin este campos-a-campos identicos, un campo
// nuevo se pierde en silencio al mapear SubFranjaProgramada -> DetalleSubFranja para el bus.
// Sin campos anidados complejos, la comparacion directa de (Nombre, Tipo) alcanza.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class SubFranjaProgramadaParidadConDetalleSubFranjaTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void SubFranjaProgramada_DeclaraLosMismosCamposQueDetalleSubFranja()
    {
        var campos = Campos<SubFranjaProgramada>();

        campos.Should().Equal(Campos<DetalleSubFranja>());
    }
}
