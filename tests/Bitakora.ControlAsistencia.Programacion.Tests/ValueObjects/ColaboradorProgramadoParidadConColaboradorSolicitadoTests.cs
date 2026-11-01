// Guardrail de la duplicacion deliberada de payload por rol (tres islas, MEF-ADR-0039 decision 2 y
// 6): ColaboradorSolicitado (DTO del body de SolicitarProgramacionTurno, Function App) y
// ColaboradorProgramado (Programacion.DomainEvents) declaran el mismo dato en dos sitios que no se
// referencian entre si. Sin este guardrail, agregar un campo a uno de los dos no rompe nada y el
// dato del body se pierde en silencio al construir el evento persistido ProgramacionTurnoSolicitada.
//
// Issue #436: releva a ColaboradorProgramadoParidadConInformacionColaboradorTests -- el gemelo que
// aquel custodiaba (InformacionColaborador, PublicEvents) murio con la reduccion del body a la
// terna, y el gemelo real del evento persistido pasa a ser el DTO del comando que lo alimenta. La
// otra mitad de la cadena (bus) la cubre ColaboradorProgramadoParidadConResumenColaboradorTests en
// ControlHoras.Tests (#433).

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ColaboradorProgramadoParidadConColaboradorSolicitadoTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void ColaboradorProgramado_DeclaraLosMismosCamposQueColaboradorSolicitado()
    {
        var campos = Campos<ColaboradorProgramado>();

        campos.Should().Equal(Campos<ColaboradorSolicitado>());
    }
}
