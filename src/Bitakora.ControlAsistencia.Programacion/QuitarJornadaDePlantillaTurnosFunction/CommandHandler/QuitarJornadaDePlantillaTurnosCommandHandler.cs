using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction.CommandHandler;

public partial class QuitarJornadaDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<QuitarJornadaDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public QuitarJornadaDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(QuitarJornadaDePlantillaTurnos command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
