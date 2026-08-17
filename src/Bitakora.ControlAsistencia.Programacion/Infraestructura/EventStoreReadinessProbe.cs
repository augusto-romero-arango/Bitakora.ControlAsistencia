using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    public Task VerificarAsync(CancellationToken ct) => throw new NotImplementedException();
}
