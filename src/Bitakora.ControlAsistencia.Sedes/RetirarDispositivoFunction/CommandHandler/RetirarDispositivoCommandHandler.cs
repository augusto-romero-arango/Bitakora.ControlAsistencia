using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;

public partial class RetirarDispositivoCommandHandler : ICommandHandlerAsync<RetirarDispositivo>
{
    private readonly IEventStore _eventStore;

    public RetirarDispositivoCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(RetirarDispositivo command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
