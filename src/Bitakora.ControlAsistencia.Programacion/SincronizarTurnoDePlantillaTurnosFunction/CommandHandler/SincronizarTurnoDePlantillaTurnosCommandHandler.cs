using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction.CommandHandler;

public partial class SincronizarTurnoDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<SincronizarTurnoDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public SincronizarTurnoDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(SincronizarTurnoDePlantillaTurnos command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
