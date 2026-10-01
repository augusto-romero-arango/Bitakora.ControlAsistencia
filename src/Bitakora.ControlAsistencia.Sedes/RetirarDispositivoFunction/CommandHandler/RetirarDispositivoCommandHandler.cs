using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;

public partial class RetirarDispositivoCommandHandler : ICommandHandlerAsync<RetirarDispositivo>
{
    private readonly IEventStore _eventStore;

    public RetirarDispositivoCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RetirarDispositivo command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new RecursoNoEncontradoException(Mensajes.SedeNoEncontrada);

        sede.RetirarDispositivo(command.DispositivoId);
    }
}
