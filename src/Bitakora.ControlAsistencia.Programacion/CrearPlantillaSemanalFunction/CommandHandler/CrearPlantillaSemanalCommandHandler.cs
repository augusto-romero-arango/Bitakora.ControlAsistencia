using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearPlantillaSemanal =
    Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CrearPlantillaSemanal;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

// La excepcion ES el canal de respuesta: InvalidOperationException -> 409 Conflict, y la
// AggregateException del factory se deja propagar -> 400 Bad Request (MEF-ADR-0004/CA-ADR-0030,
// comando HTTP sin consumidores downstream). No envolver en try/catch ni degradar a resultado.
// El BC aun no scaffoldeo la jerarquia tipada RecursoYaExisteException: migrar a ella es otro issue.
public partial class CrearPlantillaSemanalCommandHandler : ICommandHandlerAsync<ComandoCrearPlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public CrearPlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(ComandoCrearPlantillaSemanal command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<PlantillaSemanalTurnos>(command.PlantillaId, ct);
        if (existe)
            throw new InvalidOperationException(Mensajes.PlantillaYaExiste);

        var evento = PlantillaSemanalCreada.Crear(command.PlantillaId, command.Nombre, command.Semanas);
        var plantilla = PlantillaSemanalTurnos.Iniciar(evento);
        _eventStore.StartStream(plantilla);
    }
}
