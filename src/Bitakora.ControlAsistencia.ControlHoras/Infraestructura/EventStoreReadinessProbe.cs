using Marten;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    public Task VerificarAsync(CancellationToken ct) => throw new NotImplementedException();
}
