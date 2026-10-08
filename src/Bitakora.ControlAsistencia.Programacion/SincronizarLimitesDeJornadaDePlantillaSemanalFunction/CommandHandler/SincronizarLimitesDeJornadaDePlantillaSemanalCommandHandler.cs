using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction.CommandHandler;

public partial class SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<SincronizarLimitesDeJornadaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(SincronizarLimitesDeJornadaDePlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        plantilla.SincronizarLimitesDeJornada(command.JornadaId, command.Limites, command.Version);
    }
}
