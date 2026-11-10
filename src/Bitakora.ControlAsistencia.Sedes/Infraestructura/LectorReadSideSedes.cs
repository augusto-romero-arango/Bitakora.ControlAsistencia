using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Un solo adaptador para los dos puertos: BuscarUbicacionAsync es la misma consulta en ambos y no se
// duplica (MEF-ADR-0018).
public class LectorReadSideSedes(IDocumentStore store, ITenantContext tenantContext)
    : ILectorSedesParaMarcacion, ILectorUbicacionDispositivo
{
    public async Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default)
    {
        await using var session = store.QuerySession(tenantContext.TenantId);
        return await session.LoadAsync<UbicacionDispositivo>(dispositivoId, ct);
    }

    public async Task<FichaSede?> BuscarFichaSedeAsync(string sedeId, CancellationToken ct = default)
    {
        await using var session = store.QuerySession(tenantContext.TenantId);
        return await session.LoadAsync<FichaSede>(sedeId, ct);
    }
}
