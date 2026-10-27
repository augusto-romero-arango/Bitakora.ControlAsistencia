using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction.CommandHandler;

public partial class RetirarPlantillaTurnosCommandHandler : ICommandHandlerAsync<RetirarPlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public RetirarPlantillaTurnosCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(RetirarPlantillaTurnos command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
