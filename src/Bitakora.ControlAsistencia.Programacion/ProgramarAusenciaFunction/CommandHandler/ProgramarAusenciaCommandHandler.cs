using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;

public partial class ProgramarAusenciaCommandHandler : ICommandHandlerAsync<ProgramarAusencia>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ProgramarAusenciaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(ProgramarAusencia command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
