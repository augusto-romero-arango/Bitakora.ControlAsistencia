// Guardrail de la duplicacion deliberada de payload por rol (tres islas, CA-ADR-0029 decisiones #2
// y #5): ColaboradorProgramado (ControlHoras.DomainEvents, lo que persiste TurnoDiarioAsignado) y
// ResumenColaborador (PrivateEvents.Colaboradores, lo que trae el evento privado) declaran la misma
// terna en dos ensamblados que no se referencian entre si.
//
// Sin este guardrail, agregar un campo a uno de los dos no rompe nada: MapearColaboradorProgramado
// sigue compilando, el campo nuevo nunca llega al event store y el aggregate lo lee en su valor
// default -- perdida silenciosa, sin excepcion. Mismo criterio y forma que
// SedeProgramadaParidadConDetalleSedeTests.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
// Esta isla declara su propio ResumenColaborador: el alias fija cual de los dos homonimos es el que
// trae el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

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
