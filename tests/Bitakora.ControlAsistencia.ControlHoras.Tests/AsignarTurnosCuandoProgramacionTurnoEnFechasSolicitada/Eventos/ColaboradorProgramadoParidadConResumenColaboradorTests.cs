// Payload por rol (CA-ADR-0029 #5): los dos tipos viven en islas que no se referencian; sin esta
// paridad, un campo nuevo en uno de ellos se pierde en silencio en MapearColaboradorProgramado.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
// El alias fija cual de los dos ResumenColaborador homonimos trae el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

public class ColaboradorProgramadoParidadConResumenColaboradorTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void ColaboradorProgramado_DeclaraLosMismosCamposQueResumenColaborador()
    {
        var campos = Campos<ColaboradorProgramado>();

        campos.Should().Equal(Campos<ResumenColaborador>());
    }
}
