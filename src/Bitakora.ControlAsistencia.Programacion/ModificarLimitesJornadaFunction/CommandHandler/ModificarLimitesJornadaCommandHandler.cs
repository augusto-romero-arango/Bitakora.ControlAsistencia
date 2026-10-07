using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.ModificarLimitesJornada;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler;

public partial class ModificarLimitesJornadaCommandHandler(IEventStore eventStore) : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default) => throw new NotImplementedException();
}
