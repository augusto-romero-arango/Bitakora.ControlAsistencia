using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.MultiTenancy;
using Marten.Exceptions;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public class AseguradorJornadaPredeterminada(IAlmacenPreferenciasProgramacion almacen, ITenantContext tenantContext)
    : IAseguradorJornadaPredeterminada
{
    public async Task<Guid> AsegurarAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        var existentes = await almacen.LeerAsync(tenantId, ct);
        if (existentes is not null)
            return existentes.JornadaPredeterminada();

        var jornadaId = Guid.NewGuid();
        try
        {
            await almacen.IniciarAsync(
                tenantId,
                JornadaCreada.Crear(jornadaId, JornadaPredeterminadaInicial.Limites),
                new JornadaPredeterminadaAsignada(jornadaId),
                ct);
            return jornadaId;
        }
        catch (ExistingStreamIdCollisionException)
        {
            var ganadora = await almacen.LeerAsync(tenantId, ct);
            return ganadora!.JornadaPredeterminada();
        }
    }
}
