using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.ModificarVentanaDeEntrada;

namespace Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler;

public partial class ModificarVentanaDeEntradaCommandHandler(IEventStore eventStore)
    : ICommandHandlerAsync<Comando>
{
    private readonly IEventStore _eventStore = eventStore;

    public Task HandleAsync(Comando command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
