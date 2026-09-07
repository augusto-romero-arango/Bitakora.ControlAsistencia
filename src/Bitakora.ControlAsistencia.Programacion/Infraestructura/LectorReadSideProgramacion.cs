using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Adaptador unico de los lookups de solo lectura sobre el read-side propio de Programacion. Espejo
// de LectorReadSideSedes (#477): la QuerySession se abre siempre acotada al tenant que resuelve
// ITenantResolver (MEF-ADR-0028/CA-ADR-0027).
//
// Los dos puertos declaran ObtenerNombresAsync con la misma firma, asi que ambos se implementan de
// forma explicita: la clase no expone superficie publica propia y ninguna de las dos vistas queda
// arbitrariamente privilegiada como "la" del tipo concreto. Solo se resuelve por interfaz (DI).
public class LectorReadSideProgramacion(IDocumentStore store, ITenantResolver tenantResolver)
    : ILectorNombresTurno, ILectorNombresPlantillaSemanal
{
    async Task<IReadOnlyList<string>> ILectorNombresTurno.ObtenerNombresAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantResolver.TenantId);
        return await session.Query<FichaTurno>().Select(f => f.Nombre).ToListAsync(ct);
    }

    async Task<IReadOnlyList<string>> ILectorNombresPlantillaSemanal.ObtenerNombresAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantResolver.TenantId);
        return await session.Query<CuadroSemanalTurnos>().Select(c => c.Nombre).ToListAsync(ct);
    }
}
