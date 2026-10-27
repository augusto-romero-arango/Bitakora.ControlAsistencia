using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction.CommandHandler;

public partial class SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<SincronizarLimitesDeJornadaDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(SincronizarLimitesDeJornadaDePlantillaTurnos command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
