using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
// Esta isla declara su propio ResumenColaborador (payload de DepuracionDiaRecibida), homonimo del
// que trae el bus: el alias fija cual de los dos entra por el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;

// ADR-0024 decision #8: el comando equivalente seria un espejo del evento (mismos campos, sin
// semantica propia), asi que se consume directo con IPrivateEventHandlerAsync -- no se introduce un
// comando espejo. partial para admitir una clase Mensajes en archivo separado (ADR-0015).
public partial class ProgramacionTurnoDiarioSolicitadaEventHandler
    : IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ProgramacionTurnoDiarioSolicitadaEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(ProgramacionTurnoDiarioSolicitada @event, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            @event.Colaborador.CodigoColaborador, @event.Fecha);

        // El evento llega con los tipos de PrivateEvents y TurnoDiarioAsignado persiste los de
        // ControlHoras.DomainEvents. El mapeo vive aqui porque esta Function App es el unico proyecto
        // que ve las tres islas de eventos (CA-ADR-0029 decision #5, payload por rol).
        var evento = new TurnoDiarioAsignado(
            streamId, MapearColaboradorProgramado(@event.Colaborador), @event.Fecha,
            MapearTurnoDiario(@event.DetalleTurno), @event.SolicitudId);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);

        ControlDiarioAggregateRoot control;
        if (existe)
        {
            control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;
            control.AsignarTurno(evento);
        }
        else
        {
            control = ControlDiarioAggregateRoot.Iniciar(evento);
            _eventStore.StartStream(control);
        }

        // CrearDiaDepurado() debe invocarse DESPUES del Apply: lee el desglose que el Apply
        // recalcula. Se emite siempre, incluso con ControlesDeFranja vacio o todo anomalo.
        await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }

    // Mapeo campo a campo entre las dos ternas: Identificacion y NombreCompleto ya llegan
    // compuestos desde Programacion y aqui no se ensambla nada.
    private static ColaboradorProgramado MapearColaboradorProgramado(ResumenColaborador colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    private static TurnoDiario MapearTurnoDiario(DetalleTurno turno) =>
        new(turno.Nombre, turno.FranjasOrdinarias.Select(MapearFranja).ToList(), turno.Descripcion);

    // La sede que llega ya es la EFECTIVA (la cascada la resolvio en Programacion), y es tolerante
    // a null: no toda franja de un turno multi-sede trae sede resuelta.
    private static FranjaProgramada MapearFranja(DetalleFranjaOrdinaria franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList(),
            franja.Extras.Select(MapearSubFranja).ToList(),
            franja.Descripcion,
            MapearSede(franja.Sede));

    private static SedeProgramada? MapearSede(DetalleSede? sede) =>
        sede is null ? null : new SedeProgramada(sede.Id, sede.Nombre, sede.CentroDeCostos);

    private static SubFranjaProgramada MapearSubFranja(DetalleSubFranja sub) =>
        new(sub.HoraInicio, sub.HoraFin, sub.DiaOffsetInicio, sub.DiaOffsetFin, sub.Descripcion);
}
