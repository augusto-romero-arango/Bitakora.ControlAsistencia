using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction.CommandHandler;

public partial class AsignarJornadaAPlantillaTurnosCommandHandler
    : ICommandHandlerAsync<AsignarJornadaAPlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public AsignarJornadaAPlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(AsignarJornadaAPlantillaTurnos command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
