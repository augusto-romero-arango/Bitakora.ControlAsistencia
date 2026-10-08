using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoLimitesDeJornadaActualizados.EventHandler;

public class LimitesDeJornadaActualizadosEventHandler : IPrivateEventHandlerAsync<LimitesDeJornadaActualizados>
{
    private readonly ILectorPlantillasPorJornada _lector;
    private readonly ICommandRouter _commandRouter;

    public LimitesDeJornadaActualizadosEventHandler(ILectorPlantillasPorJornada lector, ICommandRouter commandRouter)
    {
        _lector = lector;
        _commandRouter = commandRouter;
    }

    public Task HandleAsync(LimitesDeJornadaActualizados @event, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
