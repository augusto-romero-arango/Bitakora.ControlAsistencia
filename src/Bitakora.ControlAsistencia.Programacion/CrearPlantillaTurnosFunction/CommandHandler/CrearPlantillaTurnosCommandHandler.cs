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

        Jornada? jornada = null;
        if (command.JornadaId is { } jornadaId)
        {
            jornada = await _eventStore.GetAggregateRootAsync<Jornada>(jornadaId, ct);
            if (jornada is null)
                throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);
            if (!jornada.Activa)
                throw new ReglaDeNegocioDeclinadaException(Mensajes.JornadaDesactivada);
        }

        var evento = PlantillaDeTurnosCreada.Crear(command.PlantillaId, command.Nombre, command.DiasDelCiclo);
        var plantilla = jornada is null
            ? PlantillaTurnos.Iniciar(evento)
            : PlantillaTurnos.IniciarConJornada(evento, command.JornadaId!.Value, jornada.Limites, jornada.Version);
        _eventStore.StartStream(plantilla);
    }
}
