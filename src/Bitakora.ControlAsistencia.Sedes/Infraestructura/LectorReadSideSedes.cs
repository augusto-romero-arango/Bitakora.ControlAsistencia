using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Adaptador unico de los lookups de solo lectura sobre el read-side propio de Sedes. Dos puertos
// segregados por feature (ILectorSedesParaMarcacion, ILectorUbicacionDispositivo) resuelven aqui:
// BuscarUbicacionAsync es la misma consulta para ambos y no se duplica (MEF-ADR-0018).
//
// La QuerySession se abre siempre acotada al tenant que resuelve ITenantResolver
// (MEF-ADR-0028/CA-ADR-0027). UbicacionDispositivo.SedeId ya es el stream key completo de la sede
// ("s:{codigo}"): se carga por Id directo, nunca partiendo ni recomponiendo ese string
// (MEF-ADR-0037/CA-ADR-0031).
public class LectorReadSideSedes(IDocumentStore store, ITenantResolver tenantResolver)
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
