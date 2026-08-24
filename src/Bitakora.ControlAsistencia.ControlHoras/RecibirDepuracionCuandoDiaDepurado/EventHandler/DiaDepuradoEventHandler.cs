using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;

// Issue #425: recibe cada foto de DiaDepurado (PrivateEvents.ControlHoras) y la traduce a los
// tipos ricos propios de ControlHoras.DomainEvents antes de entregarla al aggregate DiaCalculado
// (CA-ADR-0029 decision #5: el Function App es el unico ensamblado que ve las tres islas, asi que
// el mapeo vive aqui). Sin comando espejo: se consume directo con IPrivateEventHandlerAsync
// (MEF-ADR-0024 decision #8). Ningun consumidor nuevo: no publica ningun evento (issue #425,
// "Consumidores: ninguno nuevo").
// MEF-ADR-0009: partial class para soportar clase Mensajes en archivo separado si se requiere.
public partial class DiaDepuradoEventHandler : IPrivateEventHandlerAsync<DiaDepurado>
{
    private readonly IEventStore _eventStore;

    public DiaDepuradoEventHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task HandleAsync(DiaDepurado @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
