using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.EventHandler;

// HU-106 / issue #209: EventHandler que adiciona una marcacion al ControlDiario correspondiente.
// ADR-0024 (decision #8): MarcacionRegistrada es un IPrivateEvent intra-BC y el comando equivalente
// seria un espejo del evento (mismos campos, sin semantica propia), asi que se consume directo con
// IPrivateEventHandlerAsync<MarcacionRegistrada> - sin comando espejo.
// Trigger: MarcacionRegistrada publicado via WolverinePrivateEventSender (#105) cruza
// fisicamente el ASB interno del BC (topic marcacion-registrada, issue #212/#213) y es
// despachado a este handler por FunctionEndpoint (PrivateEventEndpointBase) via IPrivateEventRouter.
// Patron crear-o-actualizar: ExistsAsync -> si no existe StartStream, si existe GetAggregateRootAsync
// CA-9: ventana de traslape nocturno con corte a las 04:00 como constante del handler
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class MarcacionRegistradaEventHandler
    : IPrivateEventHandlerAsync<MarcacionRegistrada>
{
    private readonly IEventStore _eventStore;
    private readonly IPublicEventSender _publicEventSender;

    // CA-9: constante del handler - no del aggregate. Cuando sea configurable por empresa
    // vendra de un servicio externo, no de aqui.
    internal static readonly TimeOnly HoraCorteTraslapeNocturno = new TimeOnly(4, 0);

    public MarcacionRegistradaEventHandler(
        IEventStore eventStore,
        IPublicEventSender publicEventSender)
    {
        _eventStore = eventStore;
        _publicEventSender = publicEventSender;
    }

    public async Task HandleAsync(MarcacionRegistrada @event, CancellationToken ct = default)
    {
        var fechaCalendario = DateOnly.FromDateTime(@event.TimestampNormalizado);
        var horaDelDia = TimeOnly.FromDateTime(@event.TimestampNormalizado);

        // CA-1: fuera de la ventana nocturna la marcacion va solo al dia calendario
        // CA-2 / CA-9: dentro de la ventana [00:00, 04:00) se agrega tambien al dia anterior
        //              para cubrir turnos nocturnos que cruzan medianoche
        var fechasDestino = horaDelDia < HoraCorteTraslapeNocturno
            ? new[] { fechaCalendario, fechaCalendario.AddDays(-1) }
            : new[] { fechaCalendario };

        foreach (var fecha in fechasDestino)
        {
            await AdicionarAControlDiarioAsync(@event, fecha, ct);
        }
    }

    // Patron crear-o-actualizar con stream ID computado (EmpleadoId + Fecha).
    // CA-5: si el ControlDiario no existe se crea con Iniciar(MarcacionAdicionada).
    // CA-4: si existe, el aggregate se encarga de ignorar duplicados por minuto.
    // HU-108: tras procesar la marcacion publica DiaCalculado al topic dia-calculado
    //         via IPublicEventSender, una vez por cada fecha-destino procesada (CA-5).
    //         Idempotencia (#106): si AdicionarMarcacion ignora el duplicado, no se
    //         agrega evento al stream y por tanto no se publica DiaCalculado redundante.
    private async Task AdicionarAControlDiarioAsync(
        MarcacionRegistrada @event, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(@event.EmpleadoId, fecha);
        var evento = new MarcacionAdicionada(
            streamId,
            @event.EmpleadoId,
            @event.TimestampNormalizado,
            @event.TipoMarcacion,
            @event.DispositivoId);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);

        ControlDiarioAggregateRoot control;
        bool huboCambios;

        if (!existe)
        {
            control = ControlDiarioAggregateRoot.Iniciar(evento);
            _eventStore.StartStream(control);
            huboCambios = true;
        }
        else
        {
            control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;
            var eventosAntes = control.UncommittedEvents.Count;
            control.AdicionarMarcacion(evento);
            huboCambios = control.UncommittedEvents.Count > eventosAntes;
        }

        if (huboCambios)
            await _publicEventSender.PublishAsync(control.CrearDiaCalculado());
    }
}
