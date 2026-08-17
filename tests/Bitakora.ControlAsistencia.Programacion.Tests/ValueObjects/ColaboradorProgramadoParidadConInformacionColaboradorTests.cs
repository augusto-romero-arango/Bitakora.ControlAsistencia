// Issue #319 CA-1: guardrail de la duplicacion deliberada de payload por rol (tres islas,
// MEF-ADR-0039 decision 2 y 6): Colaborador (Programacion.DomainEvents) e InformacionColaborador
// (PublicEvents) declaran el mismo dato en dos ensamblados que no se referencian entre si.
// Sin este guardrail, agregar un campo a uno de los dos no rompe nada y el dato se pierde en
// silencio al construir el evento persistido ProgramacionTurnoSolicitada -- mismo modo de fallo
// que DetalleColaboradorParidadConInformacionColaboradorTests (issue #318) ya cubre para el payload de
// bus.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ColaboradorProgramadoParidadConInformacionColaboradorTests
{
    private static IEnumerable<(string Nombre, Type Tipo)> Campos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

    [Fact]
    public void ColaboradorProgramado_DeclaraLosMismosCamposQueInformacionColaborador()
    {
        var campos = Campos<ColaboradorProgramado>();

        campos.Should().Equal(Campos<InformacionColaborador>());
    }
}
