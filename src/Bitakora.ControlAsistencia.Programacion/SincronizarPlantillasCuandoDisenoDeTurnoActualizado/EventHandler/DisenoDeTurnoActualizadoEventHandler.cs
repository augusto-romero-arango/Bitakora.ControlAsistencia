using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction;
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

    public async Task HandleAsync(DisenoDeTurnoActualizado @event, CancellationToken ct = default)
    {
        var turno = Reinstanciar(@event);
        var plantillaIds = await _lector.ObtenerPlantillaIdsAsync(@event.TurnoId, ct);

        foreach (var plantillaId in plantillaIds)
            await _commandRouter.InvokeAsync(new SincronizarTurnoDePlantillaSemanal(
                Guid.Parse(plantillaId), @event.TurnoId, turno, @event.Version, @event.Retirado), ct);
    }

    // Los factories del VO validan: datos no reinstanciables lanzan y el mensaje va a la DLQ.
    private static Turno Reinstanciar(DisenoDeTurnoActualizado evento) =>
        Turno.Crear(evento.Nombre, evento.EsDescanso, evento.Franjas.Select(ReinstanciarFranja));

    private static FranjaOrdinaria ReinstanciarFranja(DetalleFranjaOrdinaria franja) =>
        FranjaOrdinaria.Crear(
            franja.HoraInicio,
            franja.HoraFin,
            franja.DiaOffsetFin,
            franja.Descansos.Select(ReinstanciarSubFranja),
            franja.Extras.Select(ReinstanciarSubFranja),
            franja.Sede is null
                ? null
                : new SedeProgramada(franja.Sede.Id, franja.Sede.Nombre, franja.Sede.CentroDeCostos));

    private static SubFranja ReinstanciarSubFranja(DetalleSubFranja sub) =>
        SubFranja.Crear(sub.HoraInicio, sub.HoraFin, sub.DiaOffsetInicio, sub.DiaOffsetFin);
}
