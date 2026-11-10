// Payload por rol (CA-ADR-0029 #5): sin esta paridad, un campo nuevo en uno de los dos tipos se pierde
// en silencio en MapearFranja. TurnoDiario y SubFranjaProgramada aun no tienen este guardrail aqui.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

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
