// Todos los campos son string: la igualdad por valor del record por defecto basta, sin
// Equals/GetHashCode propios.
// CrearInstancia/Copia pueblan TODOS los campos, incluidos los opcionales: con null en ambos
// lados de la comparacion, un bug de igualdad sobre ese campo pasaria inadvertido.

using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class DetalleSedeIgualdadTests : IgualdadTestBase<DetalleSede>
{
    protected override DetalleSede CrearInstancia() =>
        new("SEDE-01", "Sede Principal", "CC-100");

    protected override DetalleSede CrearInstanciaCopia() =>
        new("SEDE-01", "Sede Principal", "CC-100");

    protected override IEnumerable<(string, DetalleSede)> CrearInstanciasDiferentes()
    {
        yield return ("Id", new DetalleSede("SEDE-02", "Sede Principal", "CC-100"));
        yield return ("Nombre", new DetalleSede("SEDE-01", "Sede Norte", "CC-100"));
        yield return ("CentroDeCostos", new DetalleSede("SEDE-01", "Sede Principal", "CC-200"));
    }
}
