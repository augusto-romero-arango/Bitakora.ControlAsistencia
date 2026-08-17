using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    // Stream id centinela: nunca existe como agregado real, solo fuerza a Weasel a materializar
    // el esquema del event store (ensureStorageExistsAsync) -- la misma ruta que
    // RegistrarColaboradorCommandHandler.HandleAsync dispara vía ExistsAsync (issue #399, stack del
    // incidente). No cachea el resultado positivo entre llamadas (a diferencia de lo sugerido como
    // opcion en el issue): cachear reduciria el endpoint a una verificacion de "llego a estar listo
    // alguna vez", perdiendo la capacidad de reportar 503 si el store cae despues del arranque, y
    // ningun test exige esa optimizacion.
    private const string StreamIdCentinela = "readiness-probe-centinela";

    public async Task VerificarAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        await session.Events.FetchStreamStateAsync(StreamIdCentinela, ct);
    }
}
