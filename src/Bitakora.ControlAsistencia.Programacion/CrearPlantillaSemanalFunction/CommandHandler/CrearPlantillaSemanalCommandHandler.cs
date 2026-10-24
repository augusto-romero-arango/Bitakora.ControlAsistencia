using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearPlantillaSemanal =
    Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CrearPlantillaSemanal;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

public partial class CrearPlantillaSemanalCommandHandler : ICommandHandlerAsync<ComandoCrearPlantillaSemanal>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorNombresPlantillaSemanal _lectorNombres;

    public CrearPlantillaSemanalCommandHandler(
        IEventStore eventStore, ILectorNombresPlantillaSemanal lectorNombres)
    {
        _eventStore = eventStore;
        _lectorNombres = lectorNombres;
    }

    public async Task HandleAsync(ComandoCrearPlantillaSemanal command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<PlantillaSemanalTurnos>(command.PlantillaId, ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.PlantillaYaExiste);

        var nombresVigentes = await _lectorNombres.ObtenerNombresAsync(ct);
        if (NombreDeCatalogo.EstaRepetido(command.Nombre, nombresVigentes))
            throw new ReglaDeNegocioDeclinadaException(Mensajes.NombreDuplicado);

        Jornada? jornada = null;
        if (command.JornadaId is { } jornadaId)
        {
            jornada = await _eventStore.GetAggregateRootAsync<Jornada>(jornadaId, ct);
            if (jornada is null)
                throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);
            if (!jornada.Activa)
                throw new ReglaDeNegocioDeclinadaException(Mensajes.JornadaDesactivada);
        }

        var evento = PlantillaSemanalCreada.Crear(command.PlantillaId, command.Nombre, command.Semanas);
        var plantilla = jornada is null
            ? PlantillaSemanalTurnos.Iniciar(evento)
            : PlantillaSemanalTurnos.IniciarConJornada(
                evento, command.JornadaId!.Value, jornada.Limites, jornada.Version);
        _eventStore.StartStream(plantilla);
    }
}
