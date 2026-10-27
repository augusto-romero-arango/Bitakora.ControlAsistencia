using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;

public partial class CrearPlantillaTurnosCommandHandler : ICommandHandlerAsync<CrearPlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public CrearPlantillaTurnosCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task HandleAsync(CrearPlantillaTurnos command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
