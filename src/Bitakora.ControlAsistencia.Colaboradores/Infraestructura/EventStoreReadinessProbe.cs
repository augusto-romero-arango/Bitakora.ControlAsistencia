using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    // Nunca existe como agregado real: solo fuerza a Weasel a materializar el esquema del event
    // store (ensureStorageExistsAsync), la misma ruta que dispara ExistsAsync en
    // RegistrarColaboradorCommandHandler. No se cachea el exito: perderia el 503 si el store cae
    // despues del arranque.
    private const string StreamIdCentinela = "readiness-probe-centinela";

    public async Task VerificarAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession();
        await session.Events.FetchStreamStateAsync(StreamIdCentinela, ct);
    }
}
