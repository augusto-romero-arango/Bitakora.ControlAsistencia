using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionesCuandoDiasDepurados.EventHandler;

public partial class DiasDepuradosEventHandler : IPrivateEventHandlerAsync<DiasDepurados>
{
    private readonly IEventStore _eventStore;

    public DiasDepuradosEventHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task HandleAsync(DiasDepurados @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
