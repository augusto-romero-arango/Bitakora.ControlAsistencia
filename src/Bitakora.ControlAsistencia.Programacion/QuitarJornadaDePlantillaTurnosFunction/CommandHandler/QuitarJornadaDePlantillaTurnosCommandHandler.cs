using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction.CommandHandler;

public partial class QuitarJornadaDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<QuitarJornadaDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public QuitarJornadaDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(QuitarJornadaDePlantillaTurnos command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaTurnos>(command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var resultado = plantilla.QuitarJornada();
        if (resultado == ResultadoQuitarJornada.PlantillaRetirada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.PlantillaRetirada);
    }
}
