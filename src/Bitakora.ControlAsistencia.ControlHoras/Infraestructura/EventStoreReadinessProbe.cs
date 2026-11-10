using Marten;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    // Stream centinela que nunca existe: solo fuerza a Weasel a materializar el esquema del event
    // store (ensureStorageExistsAsync), la misma ruta que dispara la primera escritura de negocio
    // via ExistsAsync. No cachea el resultado positivo: perderia la capacidad de reportar 503 si el
    // store cae despues del arranque.
    private const string StreamIdCentinela = "readiness-probe-centinela";

    public async Task VerificarAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        await session.Events.FetchStreamStateAsync(StreamIdCentinela, ct);
    }
}
