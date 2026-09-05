using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearPlantillaSemanal =
    Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CrearPlantillaSemanal;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

// Issue #620: la excepcion ES el canal de respuesta: InvalidOperationException -> 409 Conflict, y
// la AggregateException del factory se deja propagar -> 400 Bad Request (MEF-ADR-0004/CA-ADR-0030,
// comando HTTP sin consumidores downstream). El BC no ha scaffoldeado aun la jerarquia tipada
// RecursoYaExisteException (verificado: cero referencias, #611 sigue igual) -- este handler sigue
// el patron vigente del repo (InvalidOperationException), igual que CrearTurnoCommandHandler.
// CA-3/CA-4: ExistsAsync<PlantillaSemanalTurnos> -> 409; StartStream(Iniciar(Crear(...))).
public partial class CrearPlantillaSemanalCommandHandler : ICommandHandlerAsync<ComandoCrearPlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public CrearPlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(ComandoCrearPlantillaSemanal command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
