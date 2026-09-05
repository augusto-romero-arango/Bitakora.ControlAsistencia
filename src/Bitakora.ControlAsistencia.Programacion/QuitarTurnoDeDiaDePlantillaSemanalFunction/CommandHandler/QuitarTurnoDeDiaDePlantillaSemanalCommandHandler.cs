using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction.CommandHandler;

// El BC no ha scaffoldeado aun RecursoYaExisteException/RecursoNoEncontradoException (regimen de
// coexistencia, MEF-ADR-0004): sigue el patron vigente del repo, KeyNotFoundException (404) /
// InvalidOperationException (409). SinCambios no es un rechazo (CA-ADR-0030): el handler retorna
// sin lanzar y el endpoint responde 204.
public partial class QuitarTurnoDeDiaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<QuitarTurnoDeDiaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public QuitarTurnoDeDiaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(QuitarTurnoDeDiaDePlantillaSemanal command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
