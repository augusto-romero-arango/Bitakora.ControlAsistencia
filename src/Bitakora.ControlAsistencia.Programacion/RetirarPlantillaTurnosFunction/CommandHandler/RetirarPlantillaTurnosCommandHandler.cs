using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction.CommandHandler;

public partial class RetirarPlantillaTurnosCommandHandler : ICommandHandlerAsync<RetirarPlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public RetirarPlantillaTurnosCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(RetirarPlantillaTurnos command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaTurnos>(command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        plantilla.Retirar();
    }
}
