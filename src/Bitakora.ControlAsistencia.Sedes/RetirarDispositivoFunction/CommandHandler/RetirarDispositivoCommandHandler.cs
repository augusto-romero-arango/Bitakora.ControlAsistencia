using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;

// Estado ya alcanzado (MEF-ADR-0004): dispositivo no instalado (ya retirado o nunca instalado) es
// no-op exitoso -- el aggregate declina sin mutar ni emitir, y este handler termina sin lanzar.
// Sede inexistente sigue siendo precondicion de orquestacion (KeyNotFoundException/404).
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
            throw new KeyNotFoundException(Mensajes.SedeNoEncontrada);

        sede.RetirarDispositivo(command.DispositivoId);
    }
}
