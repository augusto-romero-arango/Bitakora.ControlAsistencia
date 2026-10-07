using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler;

public partial class AsignarJornadaCommandHandler : ICommandHandlerAsync<AsignarJornada>
{
    public AsignarJornadaCommandHandler(IEventStore eventStore) => throw new NotImplementedException();

    public Task HandleAsync(AsignarJornada command, CancellationToken ct = default) => throw new NotImplementedException();
}
