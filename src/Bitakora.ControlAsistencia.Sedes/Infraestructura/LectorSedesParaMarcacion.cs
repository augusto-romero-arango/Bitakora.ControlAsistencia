using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Los dos lookups de solo lectura de la reaccion (MEF-ADR-0046 paso 2), sobre el read-side propio
// de Sedes. La QuerySession se abre siempre acotada al tenant que resuelve ITenantResolver
// (MEF-ADR-0028/CA-ADR-0027). UbicacionDispositivo.SedeId ya es el stream key completo de la sede
// ("s:{codigo}"): se carga por Id directo, nunca partiendo ni recomponiendo ese string
// (MEF-ADR-0037/CA-ADR-0031).
//
// Implementa ademas ILectorUbicacionDispositivo (issue #477): mismo lookup exacto
// (BuscarUbicacionAsync), consumido por un handler distinto (InstalarDispositivoCommandHandler) con
// su propio puerto segregado. Dos interfaces, una sola implementacion concreta -- evita duplicar la
// consulta a Marten (MEF-ADR-0018).
public class LectorSedesParaMarcacion(IDocumentStore store, ITenantResolver tenantResolver)
    : ILectorSedesParaMarcacion, ILectorUbicacionDispositivo
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
