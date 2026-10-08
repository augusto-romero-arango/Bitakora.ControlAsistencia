using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction.CommandHandler;

public partial class SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<SincronizarLimitesDeJornadaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(SincronizarLimitesDeJornadaDePlantillaSemanal command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
