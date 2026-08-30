using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;

public partial class CancelarProgramacionCommandHandler
    : ICommandHandlerAsync<CancelarProgramacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public CancelarProgramacionCommandHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(CancelarProgramacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
