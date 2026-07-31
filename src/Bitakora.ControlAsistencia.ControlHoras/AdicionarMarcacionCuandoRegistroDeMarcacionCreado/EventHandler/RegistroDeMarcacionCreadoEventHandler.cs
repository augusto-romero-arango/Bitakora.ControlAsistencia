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
// ADR-0024 (decision #8): se consume directo con IPrivateEventHandlerAsync, sin comando espejo.
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere.
public partial class RegistroDeMarcacionCreadoEventHandler
    : IPrivateEventHandlerAsync<RegistroDeMarcacionCreado>
{
    private readonly IEventStore _eventStore;
    private readonly IPublicEventSender _publicEventSender;

    public RegistroDeMarcacionCreadoEventHandler(
        IEventStore eventStore,
        IPublicEventSender publicEventSender)
    {
        _eventStore = eventStore;
        _publicEventSender = publicEventSender;
    }

    public Task HandleAsync(RegistroDeMarcacionCreado @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
