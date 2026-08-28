using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;

// Traduce el resultado declinado del aggregate a KeyNotFoundException/404 (CA-ADR-0030):
// sub-recurso inexistente, a diferencia de RetirarCentroDeCostos que es un VO singular -> 409.
// Sede inexistente cae en la misma respuesta, como precondicion de orquestacion.
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

        var resultado = sede.RetirarDispositivo(command.DispositivoId);
        if (resultado == ResultadoRetiroDispositivo.NoInstalado)
            throw new KeyNotFoundException(Mensajes.DispositivoNoInstalado);
    }
}
