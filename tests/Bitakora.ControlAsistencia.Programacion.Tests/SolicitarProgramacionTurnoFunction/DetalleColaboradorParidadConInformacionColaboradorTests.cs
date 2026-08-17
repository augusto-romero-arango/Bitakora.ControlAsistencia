// Guardrail de la duplicacion deliberada de payload por rol (CA-ADR-0029 decision #5):
// DetalleColaborador (PrivateEvents) e InformacionColaborador (PublicEvents) declaran el mismo dato en
// dos ensamblados que no se referencian, y solo esta Function App ve ambos.
// Sin este test, agregar un campo a uno de los dos no rompe nada: MapearColaborador sigue
// compilando, el campo nuevo nunca cruza el bus y el consumidor lo recibe en su valor default --
// perdida silenciosa, sin excepcion, que es el modo de fallo que ese ADR documenta.
// El JSON del cable tampoco cambia mientras la paridad se mantenga (compatibilidad del despliegue
// rolling entre productor y consumidor).

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class DetalleColaboradorParidadConInformacionColaboradorTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void DetalleColaborador_DeclaraLosMismosCamposQueInformacionColaborador()
    {
        var campos = Campos<DetalleColaborador>();

        campos.Should().Equal(Campos<InformacionColaborador>());
    }
}
