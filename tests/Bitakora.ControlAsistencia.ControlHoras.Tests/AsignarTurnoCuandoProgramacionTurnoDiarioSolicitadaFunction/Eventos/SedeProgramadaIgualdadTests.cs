// Issue #336: Tests de contrato IEquatable para SedeProgramada (record propio de
// ControlHoras.DomainEvents). Todos los campos son string: la igualdad por valor del record por
// defecto ya es correcta, sin Equals/GetHashCode custom -- mismo criterio que SedeProgramada
// (Programacion.DomainEvents, issue #331) y Colaborador (issue #319).
// Issue #462: CrearInstancia/Copia pueblan CentroDeCostos para que el campo nuevo participe
// realmente de la comparacion (con null en ambos lados, un bug de igualdad pasaria inadvertido).

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class SedeProgramadaIgualdadTests : IgualdadTestBase<SedeProgramada>
{
    protected override SedeProgramada CrearInstancia() =>
        new("SEDE-SUBA", "Suba", "CC-100");

    protected override SedeProgramada CrearInstanciaCopia() =>
        new("SEDE-SUBA", "Suba", "CC-100");

    protected override IEnumerable<(string, SedeProgramada)> CrearInstanciasDiferentes()
    {
        yield return ("Id", new SedeProgramada("SEDE-CHAPINERO", "Suba", "CC-100"));
        yield return ("Nombre", new SedeProgramada("SEDE-SUBA", "Chapinero", "CC-100"));
        yield return ("CentroDeCostos", new SedeProgramada("SEDE-SUBA", "Suba", "CC-200"));
    }
}
