using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoLimitesDeJornadaActualizados.EventHandler;

public class LimitesDeJornadaActualizadosEventHandler : IPrivateEventHandlerAsync<LimitesDeJornadaActualizados>
{
    private readonly ILectorPlantillasPorJornada _lectorPlantillasSemanales;
    private readonly ILectorPlantillasTurnosPorJornada _lectorPlantillasTurnos;
    private readonly ICommandRouter _commandRouter;

    public LimitesDeJornadaActualizadosEventHandler(
        ILectorPlantillasPorJornada lectorPlantillasSemanales, ILectorPlantillasTurnosPorJornada lectorPlantillasTurnos,
        ICommandRouter commandRouter)
    {
        _lectorPlantillasSemanales = lectorPlantillasSemanales;
        _lectorPlantillasTurnos = lectorPlantillasTurnos;
        _commandRouter = commandRouter;
    }

    public async Task HandleAsync(LimitesDeJornadaActualizados @event, CancellationToken ct = default)
    {
        var limites = Reinstanciar(@event);
        var plantillasSemanales = await _lectorPlantillasSemanales.ObtenerPlantillaIdsAsync(@event.JornadaId, ct);
        foreach (var plantillaId in plantillasSemanales)
            await _commandRouter.InvokeAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(
                Guid.Parse(plantillaId), @event.JornadaId, limites, @event.Version), ct);

        var plantillasTurnos = await _lectorPlantillasTurnos.ObtenerPlantillaIdsAsync(@event.JornadaId, ct);
        foreach (var plantillaId in plantillasTurnos)
            await _commandRouter.InvokeAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(
                Guid.Parse(plantillaId), @event.JornadaId, limites, @event.Version), ct);
    }

    // Los factories del VO validan: datos no reinstanciables lanzan y el mensaje llega a la DLQ tras agotar las entregas.
    private static LimitesJornada Reinstanciar(LimitesDeJornadaActualizados evento) =>
        LimitesJornada.Crear(
            DesdeMinutos(evento.HorasSemanalesEnMinutos),
            DesdeMinutos(evento.TopeDiarioEnMinutos),
            DesdeMinutos(evento.MinimoDiarioEnMinutos),
            evento.DiasDescansoPorSemana);

    private static HorasYMinutos DesdeMinutos(int minutos) => HorasYMinutos.Crear(minutos / 60, minutos % 60);
}
