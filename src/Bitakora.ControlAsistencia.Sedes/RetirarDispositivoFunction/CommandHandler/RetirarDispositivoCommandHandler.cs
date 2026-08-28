using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y este handler la traduce a KeyNotFoundException/404 (sub-recurso inexistente, a
// diferencia de RetirarCentroDeCostos que es un VO singular -> 409). Sede inexistente es
// precondicion de orquestacion (KeyNotFoundException/404), sin evento de fallo persistido.
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
