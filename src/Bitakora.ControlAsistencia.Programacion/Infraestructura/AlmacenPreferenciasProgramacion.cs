using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Sesion propia acotada al tenant: el asegurador confirma su transaccion antes de que siga quien lo llamo.
public class AlmacenPreferenciasProgramacion(IDocumentStore store) : IAlmacenPreferenciasProgramacion
{
    public async Task<PreferenciasProgramacion?> LeerAsync(string tenantId, CancellationToken ct)
    {
        await using var session = store.LightweightSession(tenantId);
        return await session.Events.AggregateStreamAsync<PreferenciasProgramacion>(
            PreferenciasProgramacion.ComputarStreamId(tenantId), token: ct);
    }

    public async Task IniciarAsync(string tenantId, JornadaCreada jornada, JornadaPredeterminadaAsignada asignacion,
        CancellationToken ct)
    {
        await using var session = store.LightweightSession(tenantId);
        session.Events.StartStream<Jornada>(jornada.JornadaId.ToString(), jornada);
        session.Events.StartStream<PreferenciasProgramacion>(
            PreferenciasProgramacion.ComputarStreamId(tenantId), asignacion);
        await session.SaveChangesAsync(ct);
    }
}
