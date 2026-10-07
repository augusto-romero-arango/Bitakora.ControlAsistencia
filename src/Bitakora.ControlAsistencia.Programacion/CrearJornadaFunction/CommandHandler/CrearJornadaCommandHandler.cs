using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CrearJornada;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;

public partial class CrearJornadaCommandHandler(IEventStore eventStore) : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default) => throw new NotImplementedException();
}
