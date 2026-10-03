using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarAusenciaCuandoAusenciaDiariaProgramada.EventHandler;

public partial class AusenciaDiariaProgramadaEventHandler : IPrivateEventHandlerAsync<AusenciaDiariaProgramada>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public AusenciaDiariaProgramadaEventHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(AusenciaDiariaProgramada @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
