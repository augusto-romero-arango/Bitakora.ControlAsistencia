using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
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

    public async Task HandleAsync(AusenciaDiariaCancelada @event, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            @event.Colaborador.CodigoColaborador, @event.Fecha);
        var evento = CancelacionAusenciaDiariaRegistrada.Crear(streamId, @event.AusenciaId, @event.Fecha);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);
        if (existe)
        {
            var control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;
            if (control.CancelarAusencia(evento) == ResultadoCancelarAusencia.Liberado)
                await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
        }
        else
        {
            _eventStore.StartStream(ControlDiarioAggregateRoot.Iniciar(evento));
        }
    }
}
