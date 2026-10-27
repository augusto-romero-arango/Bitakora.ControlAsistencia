using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction.CommandHandler;

public partial class AsignarTurnoADiaDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<AsignarTurnoADiaDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public AsignarTurnoADiaDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(AsignarTurnoADiaDePlantillaTurnos command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
