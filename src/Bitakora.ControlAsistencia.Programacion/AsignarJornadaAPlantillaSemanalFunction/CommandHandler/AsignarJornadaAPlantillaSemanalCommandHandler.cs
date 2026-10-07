using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction.CommandHandler;

public partial class AsignarJornadaAPlantillaSemanalCommandHandler
    : ICommandHandlerAsync<AsignarJornadaAPlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public AsignarJornadaAPlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(AsignarJornadaAPlantillaSemanal command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
