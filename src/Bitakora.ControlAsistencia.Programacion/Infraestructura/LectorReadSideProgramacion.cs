using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Adaptador del lookup de solo lectura sobre el read-side propio de Programacion
// (ILectorNombresTurno). Espejo de LectorReadSideSedes (#477): la QuerySession se abre siempre
// acotada al tenant que resuelve ITenantResolver (MEF-ADR-0028/CA-ADR-0027).
public class LectorReadSideProgramacion(IDocumentStore store, ITenantResolver tenantResolver)
    : ILectorNombresTurno
{
    public Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();
}
