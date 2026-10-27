using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction.CommandHandler;

public partial class SincronizarTurnoDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<SincronizarTurnoDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public SincronizarTurnoDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(SincronizarTurnoDePlantillaTurnos command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaTurnos>(
            command.PlantillaId.ToString(), ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        plantilla.SincronizarTurno(command.TurnoId, command.Turno, command.Version, command.Retirado);
    }
}
