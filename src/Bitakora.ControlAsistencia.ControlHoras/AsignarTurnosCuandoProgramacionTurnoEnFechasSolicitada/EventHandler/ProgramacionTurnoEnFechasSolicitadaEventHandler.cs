using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.EventHandler;

public partial class ProgramacionTurnoEnFechasSolicitadaEventHandler
    : IPrivateEventHandlerAsync<ProgramacionTurnoEnFechasSolicitada>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ProgramacionTurnoEnFechasSolicitadaEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(ProgramacionTurnoEnFechasSolicitada @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
