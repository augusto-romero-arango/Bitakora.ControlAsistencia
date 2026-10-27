using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction.CommandHandler;

public partial class SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<SincronizarLimitesDeJornadaDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(SincronizarLimitesDeJornadaDePlantillaTurnos command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaTurnos>(command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        plantilla.SincronizarLimitesDeJornada(command.JornadaId, command.Limites, command.Version);
    }
}
