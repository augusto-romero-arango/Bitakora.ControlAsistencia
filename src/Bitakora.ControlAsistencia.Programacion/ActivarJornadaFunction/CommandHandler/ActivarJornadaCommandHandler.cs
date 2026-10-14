using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction.ActivarJornada;

namespace Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction.CommandHandler;

public partial class ActivarJornadaCommandHandler(IEventStore eventStore) : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
