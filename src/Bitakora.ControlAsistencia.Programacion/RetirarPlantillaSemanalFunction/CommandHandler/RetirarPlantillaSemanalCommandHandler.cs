using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction.CommandHandler;

// El BC no ha scaffoldeado aun RecursoYaExisteException/RecursoNoEncontradoException (regimen de
// coexistencia, MEF-ADR-0004): sigue el patron vigente del repo, KeyNotFoundException (404).
// SinCambios no es un rechazo (CA-ADR-0030, harness#850): el handler retorna sin lanzar y el
// endpoint responde 204.
public partial class RetirarPlantillaSemanalCommandHandler : ICommandHandlerAsync<RetirarPlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public RetirarPlantillaSemanalCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(RetirarPlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new KeyNotFoundException(Mensajes.PlantillaNoEncontrada);

        plantilla.Retirar();
    }
}
