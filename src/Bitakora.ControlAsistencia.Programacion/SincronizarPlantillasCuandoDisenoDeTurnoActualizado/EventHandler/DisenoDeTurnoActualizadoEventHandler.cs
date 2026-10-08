using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado.EventHandler;

public class DisenoDeTurnoActualizadoEventHandler : IPrivateEventHandlerAsync<DisenoDeTurnoActualizado>
{
    private readonly ILectorPlantillasPorTurno _lector;
    private readonly ICommandRouter _commandRouter;

    public DisenoDeTurnoActualizadoEventHandler(ILectorPlantillasPorTurno lector, ICommandRouter commandRouter)
    {
        _lector = lector;
        _commandRouter = commandRouter;
    }

    public Task HandleAsync(DisenoDeTurnoActualizado @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
