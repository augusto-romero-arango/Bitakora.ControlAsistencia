using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction.CommandHandler;

public partial class QuitarJornadaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<QuitarJornadaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public QuitarJornadaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(QuitarJornadaDePlantillaSemanal command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
