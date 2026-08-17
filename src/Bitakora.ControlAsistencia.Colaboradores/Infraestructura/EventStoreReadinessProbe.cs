using Marten;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

public class EventStoreReadinessProbe(IDocumentStore store) : IEventStoreReadinessProbe
{
    public Task VerificarAsync(CancellationToken ct) => throw new NotImplementedException();
}
