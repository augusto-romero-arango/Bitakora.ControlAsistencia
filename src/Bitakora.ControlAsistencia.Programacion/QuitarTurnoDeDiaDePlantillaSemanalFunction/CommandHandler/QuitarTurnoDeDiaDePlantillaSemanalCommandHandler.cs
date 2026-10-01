using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction.CommandHandler;

public partial class QuitarTurnoDeDiaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<QuitarTurnoDeDiaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public QuitarTurnoDeDiaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(QuitarTurnoDeDiaDePlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var resultado = plantilla.QuitarDia(command.Semana, command.Dia);
        switch (resultado)
        {
            case ResultadoQuitarDia.PlantillaRetirada:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.PlantillaRetirada);
            case ResultadoQuitarDia.SemanaFueraDeRango:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.SemanaFueraDeRango);
        }
    }
}
