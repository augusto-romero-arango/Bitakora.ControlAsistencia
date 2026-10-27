using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;

public partial class CrearPlantillaTurnosCommandHandler : ICommandHandlerAsync<CrearPlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public CrearPlantillaTurnosCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task HandleAsync(CrearPlantillaTurnos command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<PlantillaTurnos>(command.PlantillaId, ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.PlantillaYaExiste);

        var evento = PlantillaDeTurnosCreada.Crear(command.PlantillaId, command.Nombre, command.DiasDelCiclo);
        _eventStore.StartStream(PlantillaTurnos.Iniciar(evento));
    }
}
