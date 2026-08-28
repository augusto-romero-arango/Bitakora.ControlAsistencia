using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction.CommandHandler;

// Mecanismo "declinar con resultado" (CA-ADR-0030): sin CC vigente el aggregate declina sin mutar
// ni emitir, y este handler traduce esa razon a InvalidOperationException/409. Sede inexistente es
// precondicion de orquestacion (KeyNotFoundException/404), sin evento de fallo persistido.
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
            throw new KeyNotFoundException(Mensajes.SedeNoEncontrada);

        var resultado = sede.RetirarCentroDeCostos();
        if (resultado == ResultadoRetiroCentroDeCostos.SinCentroDeCostosVigente)
            throw new InvalidOperationException(Mensajes.SinCentroDeCostosVigente);
    }
}
