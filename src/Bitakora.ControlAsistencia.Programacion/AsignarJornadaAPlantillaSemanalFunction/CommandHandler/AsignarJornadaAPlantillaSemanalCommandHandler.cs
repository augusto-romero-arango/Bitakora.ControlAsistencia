using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction.CommandHandler;

public partial class AsignarJornadaAPlantillaSemanalCommandHandler
    : ICommandHandlerAsync<AsignarJornadaAPlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public AsignarJornadaAPlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarJornadaAPlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var jornada = await _eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId, ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        var resultado = plantilla.AsignarJornada(command.JornadaId, jornada.Limites, jornada.Version);
        if (resultado == ResultadoAsignarJornada.PlantillaRetirada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.PlantillaRetirada);
    }
}
