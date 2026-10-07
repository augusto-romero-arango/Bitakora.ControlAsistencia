using Cosmos.MultiTenancy;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public class AseguradorJornadaPredeterminada(IAlmacenPreferenciasProgramacion almacen, ITenantContext tenantContext)
    : IAseguradorJornadaPredeterminada
{
    public Task<Guid> AsegurarAsync(CancellationToken ct) => throw new NotImplementedException();
}
