using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction.CommandHandler;

public partial class AsignarJornadaAPlantillaTurnosCommandHandler
    : ICommandHandlerAsync<AsignarJornadaAPlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public AsignarJornadaAPlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarJornadaAPlantillaTurnos command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaTurnos>(command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var jornada = await _eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId, ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        if (plantilla.JornadaId != command.JornadaId && !jornada.Activa)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.JornadaDesactivada);

        var resultado = plantilla.AsignarJornada(command.JornadaId, jornada.Limites, jornada.Version);
        if (resultado == ResultadoAsignarJornada.PlantillaRetirada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.PlantillaRetirada);
    }
}
