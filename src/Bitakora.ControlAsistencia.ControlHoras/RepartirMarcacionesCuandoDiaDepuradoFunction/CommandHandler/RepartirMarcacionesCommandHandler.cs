using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction.CommandHandler;

public partial class RepartirMarcacionesCommandHandler : ICommandHandlerAsync<RepartirMarcaciones>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RepartirMarcacionesCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(RepartirMarcaciones command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
