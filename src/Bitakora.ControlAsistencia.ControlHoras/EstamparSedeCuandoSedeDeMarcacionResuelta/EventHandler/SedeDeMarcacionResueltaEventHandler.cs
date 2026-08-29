using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler;

// Issue #463: cierre del enriquecimiento coreografiado (MEF-ADR-0046) -- Sedes resolvio
// dispositivo->sede->CC y lo publico como SedeDeMarcacionResuelta (#467); ControlHoras -- dueno de
// la marcacion -- graba ese resultado como un hecho mas de su historia (SedeDeMarcacionIdentificada,
// ControlHoras.DomainEvents). Se consume directo con IPrivateEventHandlerAsync, sin comando espejo
// (MEF-ADR-0024 decision #8).
// MEF-ADR-0009: partial class para soportar la clase Mensajes en archivo separado.
// Stub de compilacion -- fase roja del pipeline TDD: la orquestacion real (fechas-destino con
// traslape nocturno, precondicion de ControlDiario/marcacion existente que dispara el retry del
// bus, invocacion de EstamparSede y re-publicacion condicional de DiaDepurado) es responsabilidad
// del implementer.
public partial class SedeDeMarcacionResueltaEventHandler
    : IPrivateEventHandlerAsync<SedeDeMarcacionResuelta>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public SedeDeMarcacionResueltaEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(SedeDeMarcacionResuelta @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
