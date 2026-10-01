using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction.CommandHandler;

public partial class RetirarCentroDeCostosCommandHandler : ICommandHandlerAsync<RetirarCentroDeCostos>
{
    private readonly IEventStore _eventStore;

    public RetirarCentroDeCostosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RetirarCentroDeCostos command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new RecursoNoEncontradoException(Mensajes.SedeNoEncontrada);

        sede.RetirarCentroDeCostos();
    }
}
