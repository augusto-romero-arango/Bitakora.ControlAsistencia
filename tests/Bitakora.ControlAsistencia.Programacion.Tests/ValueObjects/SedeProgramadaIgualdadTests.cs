// Issue #331: Tests de contrato IEquatable para SedeProgramada (record propio de
// Programacion.DomainEvents). Todos los campos son string: la igualdad por valor del record por
// defecto ya es correcta, sin Equals/GetHashCode custom -- mismo criterio que Colaborador (issue #319).

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class SedeProgramadaIgualdadTests : IgualdadTestBase<SedeProgramada>
{
    protected override SedeProgramada CrearInstancia() =>
        new("SEDE-01", "Sede Principal");

    protected override SedeProgramada CrearInstanciaCopia() =>
        new("SEDE-01", "Sede Principal");

    protected override IEnumerable<(string, SedeProgramada)> CrearInstanciasDiferentes()
    {
        yield return ("Id", new SedeProgramada("SEDE-02", "Sede Principal"));
        yield return ("Nombre", new SedeProgramada("SEDE-01", "Sede Norte"));
    }
}
