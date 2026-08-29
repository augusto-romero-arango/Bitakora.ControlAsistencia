using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Issue #467: implementacion real de ILectorSedesParaMarcacion -- los dos lookups de solo lectura
// de MEF-ADR-0046 paso 2, sobre el mismo read-side y la misma QuerySession acotada a tenant que
// ObtenerFichaSede/ListarFichasSede (MEF-ADR-0028/CA-ADR-0027). UbicacionDispositivo.SedeId ya
// guarda el stream key completo de la sede ("s:{codigo}") -- se carga directo por Id, sin partir ni
// recomponer strings (MEF-ADR-0037/CA-ADR-0031).
public class LectorSedesParaMarcacion(IDocumentStore store, ITenantResolver tenantResolver)
    : ILectorSedesParaMarcacion
{
    public async Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default)
    {
        await using var session = store.QuerySession(tenantResolver.TenantId);
        return await session.LoadAsync<UbicacionDispositivo>(dispositivoId, ct);
    }

    public async Task<FichaSede?> BuscarFichaSedeAsync(string sedeId, CancellationToken ct = default)
    {
        await using var session = store.QuerySession(tenantResolver.TenantId);
        return await session.LoadAsync<FichaSede>(sedeId, ct);
    }
}
