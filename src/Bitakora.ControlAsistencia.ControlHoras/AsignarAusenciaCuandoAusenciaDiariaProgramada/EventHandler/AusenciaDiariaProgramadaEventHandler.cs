using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
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

    public async Task HandleAsync(AusenciaDiariaProgramada @event, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            @event.Colaborador.CodigoColaborador, @event.Fecha);

        var evento = AusenciaDiariaAsignada.Crear(
            streamId,
            new ColaboradorProgramado(
                @event.Colaborador.Identificacion,
                @event.Colaborador.CodigoColaborador,
                @event.Colaborador.NombreCompleto),
            @event.Fecha,
            @event.AusenciaId,
            @event.Motivo);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);

        ControlDiarioAggregateRoot control;
        if (existe)
        {
            control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;
            if (control.AsignarAusencia(evento) != ResultadoAsignarAusencia.Asignada)
                return;
        }
        else
        {
            control = ControlDiarioAggregateRoot.Iniciar(evento);
            _eventStore.StartStream(control);
        }

        await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }
}
