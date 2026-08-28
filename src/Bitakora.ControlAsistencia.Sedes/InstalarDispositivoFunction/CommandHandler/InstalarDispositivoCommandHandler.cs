using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler;

public partial class InstalarDispositivoCommandHandler : ICommandHandlerAsync<InstalarDispositivo>
{
    private readonly IEventStore _eventStore;

    public InstalarDispositivoCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(InstalarDispositivo command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
