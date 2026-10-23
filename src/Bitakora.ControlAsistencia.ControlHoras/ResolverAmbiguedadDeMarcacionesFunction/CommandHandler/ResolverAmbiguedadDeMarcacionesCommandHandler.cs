using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction.CommandHandler;

public partial class ResolverAmbiguedadDeMarcacionesCommandHandler : ICommandHandlerAsync<ResolverAmbiguedadDeMarcaciones>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ResolverAmbiguedadDeMarcacionesCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(ResolverAmbiguedadDeMarcaciones command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
