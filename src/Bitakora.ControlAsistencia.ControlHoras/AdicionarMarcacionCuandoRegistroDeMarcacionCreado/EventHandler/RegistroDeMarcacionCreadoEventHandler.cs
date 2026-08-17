using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;

// Issue #270: reemplaza a MarcacionRegistradaEventHandler (folder
// AdicionarMarcacionCuandoMarcacionRegistrada, retirado por este issue). El contrato que cruza el
// ASB interno del BC ahora es RegistroDeMarcacionCreado (PrivateEvents.ControlHoras), no
// MarcacionRegistrada (que dejo de implementar IPrivateEvent - CA-3). Paridad de campos identica,
// asi que el comportamiento (patron crear-o-actualizar sobre ControlDiario, ventana de traslape
// nocturno, publicacion de DiaCalculado) se preserva sobre el tipo nuevo (CA-5).
// MEF-ADR-0024 (decision #8): se consume directo con IPrivateEventHandlerAsync, sin comando espejo.
// MEF-ADR-0009: partial class para soportar clase Mensajes en archivo separado si se requiere.
public partial class RegistroDeMarcacionCreadoEventHandler
    : IPrivateEventHandlerAsync<RegistroDeMarcacionCreado>
{
    private readonly IEventStore _eventStore;
    private readonly IPublicEventSender _publicEventSender;

    // CA-9: constante del handler - no del aggregate. Cuando sea configurable por empresa
    // vendra de un servicio externo, no de aqui.
    internal static readonly TimeOnly HoraCorteTraslapeNocturno = new(4, 0);

    public RegistroDeMarcacionCreadoEventHandler(
        IEventStore eventStore,
        IPublicEventSender publicEventSender)
    {
        _eventStore = eventStore;
        _publicEventSender = publicEventSender;
    }

    public async Task HandleAsync(RegistroDeMarcacionCreado @event, CancellationToken ct = default)
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

    // Patron crear-o-actualizar con stream ID computado (CodigoColaborador + Fecha).
    // CA-5: si el ControlDiario no existe se crea con Iniciar(MarcacionAdicionada).
    // CA-4: si existe, el aggregate se encarga de ignorar duplicados por minuto.
    // HU-108: tras procesar la marcacion publica DiaCalculado al topic dia-calculado
    //         via IPublicEventSender, una vez por cada fecha-destino procesada (CA-5).
    //         Idempotencia (#106): si AdicionarMarcacion ignora el duplicado, no se
    //         agrega evento al stream y por tanto no se publica DiaCalculado redundante.
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
            await _publicEventSender.PublishAsync(control.CrearDiaCalculado());
    }
}
