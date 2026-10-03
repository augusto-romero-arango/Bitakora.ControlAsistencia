using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.CancelarAusenciaCuandoAusenciaDiariaCancelada.EventHandler;

public class AusenciaDiariaCanceladaEventHandler : IPrivateEventHandlerAsync<AusenciaDiariaCancelada>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public AusenciaDiariaCanceladaEventHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(AusenciaDiariaCancelada @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
