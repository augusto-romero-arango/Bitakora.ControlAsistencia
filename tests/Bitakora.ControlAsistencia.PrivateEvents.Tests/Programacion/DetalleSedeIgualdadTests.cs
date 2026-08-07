// Issue #331: Tests de contrato IEquatable para DetalleSede, por simetria con la familia
// Detalle*IgualdadTests. Todos los campos son string: la igualdad por valor del record por
// defecto ya es correcta, sin Equals/GetHashCode propios (MEF-ADR-0012) -- mismo criterio que
// DetalleEmpleado.

using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleSedeIgualdadTests : IgualdadTestBase<DetalleSede>
{
    protected override DetalleSede CrearInstancia() =>
        new("SEDE-01", "Sede Principal");

    protected override DetalleSede CrearInstanciaCopia() =>
        new("SEDE-01", "Sede Principal");

    protected override IEnumerable<(string, DetalleSede)> CrearInstanciasDiferentes()
    {
        yield return ("Id", new DetalleSede("SEDE-02", "Sede Principal"));
        yield return ("Nombre", new DetalleSede("SEDE-01", "Sede Norte"));
    }
}
