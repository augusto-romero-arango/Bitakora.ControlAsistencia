using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction.CommandHandler;

// El BC no ha scaffoldeado aun RecursoYaExisteException/RecursoNoEncontradoException (regimen de
// coexistencia, MEF-ADR-0004): sigue el patron vigente del repo, KeyNotFoundException (404) /
// InvalidOperationException (409). SinCambios no es un rechazo (CA-ADR-0030): el handler retorna
// sin lanzar y el endpoint responde 204. En el camino de exito el aggregate deja el evento en
// _uncommittedEvents -- el middleware persiste via SaveChanges.
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
            throw new KeyNotFoundException(Mensajes.PlantillaNoEncontrada);

        var resultado = plantilla.QuitarDia(command.Semana, command.Dia);
        switch (resultado)
        {
            case ResultadoQuitarDia.PlantillaRetirada:
                throw new InvalidOperationException(Mensajes.PlantillaRetirada);
            case ResultadoQuitarDia.SemanaFueraDeRango:
                throw new InvalidOperationException(Mensajes.SemanaFueraDeRango);
        }
    }
}
