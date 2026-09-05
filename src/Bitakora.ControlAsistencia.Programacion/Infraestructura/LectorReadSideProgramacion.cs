using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Adaptador del lookup de solo lectura sobre el read-side propio de Programacion
// (ILectorNombresTurno, ILectorNombresPlantillaSemanal). Espejo de LectorReadSideSedes (#477): la
// QuerySession se abre siempre acotada al tenant que resuelve ITenantResolver
// (MEF-ADR-0028/CA-ADR-0027).
public class LectorReadSideProgramacion(IDocumentStore store, ITenantResolver tenantResolver)
    : ILectorNombresTurno, ILectorNombresPlantillaSemanal
{
    public async Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default)
    {
        await using var session = store.QuerySession(tenantResolver.TenantId);
        return await session.Query<FichaTurno>().Select(f => f.Nombre).ToListAsync(ct);
    }

    Task<IReadOnlyList<string>> ILectorNombresPlantillaSemanal.ObtenerNombresAsync(CancellationToken ct) =>
        throw new NotImplementedException();
}
