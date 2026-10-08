using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction.CommandHandler;

public partial class SincronizarTurnoDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<SincronizarTurnoDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public SincronizarTurnoDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(SincronizarTurnoDePlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        plantilla.SincronizarTurno(command.TurnoId, command.Turno, command.Version, command.Retirado);
    }
}
