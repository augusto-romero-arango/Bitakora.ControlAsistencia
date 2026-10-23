using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;

// La marcacion se guarda solo en su dia calendario; RepartirMarcacionesCuandoDiaDepurado la entrega a
// los vecinos cuya cobertura la contiene (CA-ADR-0042).
// MEF-ADR-0024 (decision #8): se consume directo con IPrivateEventHandlerAsync, sin comando espejo.
// MEF-ADR-0009: partial class para soportar clase Mensajes en archivo separado si se requiere.
public partial class RegistroDeMarcacionCreadoEventHandler
    : IPrivateEventHandlerAsync<RegistroDeMarcacionCreado>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RegistroDeMarcacionCreadoEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(RegistroDeMarcacionCreado @event, CancellationToken ct = default)
    {
        var fechaCalendario = DateOnly.FromDateTime(@event.TimestampNormalizado);
        await AdicionarAControlDiarioAsync(@event, fechaCalendario, ct);
    }

    // Crear-o-actualizar: si AdicionarMarcacion ignora el duplicado por minuto no se publica
    // DiaDepurado (evita un reparto redundante).
    private async Task AdicionarAControlDiarioAsync(
        RegistroDeMarcacionCreado @event, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(@event.CodigoColaborador, fecha);
        var evento = new MarcacionAdicionada(
            streamId,
            @event.CodigoColaborador,
            @event.TimestampNormalizado,
            @event.TipoMarcacion,
            @event.DispositivoId);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);

        ControlDiarioAggregateRoot control;
        bool huboCambios;

        if (existe)
        {
            control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;
            var eventosAntes = control.UncommittedEvents.Count;
            control.AdicionarMarcacion(evento);
            huboCambios = control.UncommittedEvents.Count > eventosAntes;
        }
        else
        {
            control = ControlDiarioAggregateRoot.Iniciar(evento);
            _eventStore.StartStream(control);
            huboCambios = true;
        }

        if (huboCambios)
        {
            var diaDepurado = control.CrearDiaDepurado();
            await _privateEventSender.PublishAsync(new PublishOptions { GroupId = diaDepurado.CodigoColaborador }, diaDepurado);
        }
    }
}
