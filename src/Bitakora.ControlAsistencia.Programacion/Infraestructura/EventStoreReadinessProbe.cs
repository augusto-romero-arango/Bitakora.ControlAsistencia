using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    // Stream centinela que nunca existe: fuerza a Weasel a materializar el esquema del event store por
    // la misma ruta que la primera escritura de negocio. No se cachea el positivo: el endpoint debe
    // poder reportar 503 si el store cae despues del arranque.
    private const string StreamIdCentinela = "readiness-probe-centinela";

    public async Task VerificarAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        await session.Events.FetchStreamStateAsync(StreamIdCentinela, ct);
    }
}
