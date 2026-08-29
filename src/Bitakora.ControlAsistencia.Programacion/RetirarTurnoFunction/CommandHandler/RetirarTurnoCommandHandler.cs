using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;

public partial class RetirarTurnoCommandHandler : ICommandHandlerAsync<RetirarTurno>
{
    private readonly IEventStore _eventStore;

    public RetirarTurnoCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(RetirarTurno command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
