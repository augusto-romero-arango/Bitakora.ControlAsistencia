using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction.CommandHandler;

// Sede inexistente se declina con KeyNotFoundException (el endpoint la traduce a 404), sin
// persistir ningun evento de fallo -- CA-ADR-0030. Asignar por primera vez y reemplazar son el
// mismo comando: sin variante de idempotencia silenciosa (a diferencia de AsignarEtiqueta).
public partial class AsignarCentroDeCostosCommandHandler : ICommandHandlerAsync<AsignarCentroDeCostos>
{
    private readonly IEventStore _eventStore;

    public AsignarCentroDeCostosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarCentroDeCostos command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new KeyNotFoundException(Mensajes.SedeNoEncontrada);

        sede.AsignarCentroDeCostos(command.CentroDeCostos);
    }
}
