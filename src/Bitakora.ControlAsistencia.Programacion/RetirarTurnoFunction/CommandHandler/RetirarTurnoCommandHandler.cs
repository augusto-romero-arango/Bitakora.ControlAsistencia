using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;

public partial class RetirarTurnoCommandHandler : ICommandHandlerAsync<RetirarTurno>
{
    private readonly IEventStore _eventStore;

    public RetirarTurnoCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(RetirarTurno command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        var resultado = catalogo.Retirar();
        if (resultado == ResultadoRetiroTurno.YaEstabaRetirado)
            throw new InvalidOperationException(Mensajes.TurnoYaRetirado);
    }
}
