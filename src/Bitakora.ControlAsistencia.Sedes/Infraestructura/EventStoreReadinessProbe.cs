using Marten;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    // Stream id centinela: nunca existe como agregado real, solo fuerza a Weasel a materializar
    // el esquema del event store (ensureStorageExistsAsync) -- la misma ruta que un command
    // handler que dispara ExistsAsync (issue #399, patron replicado de Colaboradores/ControlHoras/
    // Programacion). No cachea el resultado positivo entre llamadas: cachear reduciria el endpoint
    // a una verificacion de "llego a estar listo alguna vez", perdiendo la capacidad de reportar
    // 503 si el store cae despues del arranque.
    private const string StreamIdCentinela = "readiness-probe-centinela";

    public async Task VerificarAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        await session.Events.FetchStreamStateAsync(StreamIdCentinela, ct);
    }
}
