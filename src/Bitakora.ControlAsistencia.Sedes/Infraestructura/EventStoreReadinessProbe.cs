using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    // Stream que nunca existe: solo fuerza a Weasel a materializar el esquema del event store, la
    // misma ruta que un ExistsAsync de un command handler. No cachear el resultado: el endpoint debe
    // poder reportar 503 si el store cae despues del arranque.
    private const string StreamIdCentinela = "readiness-probe-centinela";

    public async Task VerificarAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        await session.Events.FetchStreamStateAsync(StreamIdCentinela, ct);
    }
}
