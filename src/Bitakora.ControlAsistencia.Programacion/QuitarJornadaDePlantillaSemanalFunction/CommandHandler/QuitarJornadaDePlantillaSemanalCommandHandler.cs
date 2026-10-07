using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction.CommandHandler;

public partial class QuitarJornadaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<QuitarJornadaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public QuitarJornadaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(QuitarJornadaDePlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var resultado = plantilla.QuitarJornada();
        if (resultado == ResultadoQuitarJornada.PlantillaRetirada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.PlantillaRetirada);
    }
}
