using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;

public partial class CancelarAusenciaCommandHandler : ICommandHandlerAsync<CancelarAusencia>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public CancelarAusenciaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(CancelarAusencia command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
