using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction.CommandHandler;

public partial class RetirarPlantillaSemanalCommandHandler : ICommandHandlerAsync<RetirarPlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public RetirarPlantillaSemanalCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(RetirarPlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        plantilla.Retirar();
    }
}
