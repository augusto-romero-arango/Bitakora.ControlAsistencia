using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction.CommandHandler;

public partial class SincronizarTurnoDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<SincronizarTurnoDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public SincronizarTurnoDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(SincronizarTurnoDePlantillaSemanal command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
