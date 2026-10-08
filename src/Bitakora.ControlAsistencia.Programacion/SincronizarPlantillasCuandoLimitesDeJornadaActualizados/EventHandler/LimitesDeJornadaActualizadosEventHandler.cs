using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction;
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

    public async Task HandleAsync(LimitesDeJornadaActualizados @event, CancellationToken ct = default)
    {
        var limites = Reinstanciar(@event);
        var plantillaIds = await _lector.ObtenerPlantillaIdsAsync(@event.JornadaId, ct);

        foreach (var plantillaId in plantillaIds)
            await _commandRouter.InvokeAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(
                Guid.Parse(plantillaId), @event.JornadaId, limites, @event.Version), ct);
    }

    // Los factories del VO validan: datos no reinstanciables lanzan y el mensaje va a la DLQ.
    private static LimitesJornada Reinstanciar(LimitesDeJornadaActualizados evento) =>
        LimitesJornada.Crear(
            DesdeMinutos(evento.HorasSemanalesEnMinutos),
            DesdeMinutos(evento.TopeDiarioEnMinutos),
            DesdeMinutos(evento.MinimoDiarioEnMinutos),
            evento.DiasDescansoPorSemana);

    private static HorasYMinutos DesdeMinutos(int minutos) => HorasYMinutos.Crear(minutos / 60, minutos % 60);
}
