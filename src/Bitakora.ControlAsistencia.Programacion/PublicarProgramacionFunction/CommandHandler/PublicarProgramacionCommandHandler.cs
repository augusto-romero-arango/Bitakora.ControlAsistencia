using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction.CommandHandler;

public partial class PublicarProgramacionCommandHandler : ICommandHandlerAsync<PublicarProgramacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public PublicarProgramacionCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(PublicarProgramacion command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
