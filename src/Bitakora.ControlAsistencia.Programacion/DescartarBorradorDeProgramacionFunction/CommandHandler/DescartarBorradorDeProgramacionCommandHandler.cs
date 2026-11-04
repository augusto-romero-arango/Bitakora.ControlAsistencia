using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.CommandHandler;

public partial class DescartarBorradorDeProgramacionCommandHandler : ICommandHandlerAsync<DescartarBorradorDeProgramacion>
{
    private readonly IEventStore _eventStore;

    public DescartarBorradorDeProgramacionCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task HandleAsync(DescartarBorradorDeProgramacion command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
