using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;

// HU-12 / issue #210: EventHandler que asigna el turno diario al ControlDiario cuando llega
//   ProgramacionTurnoDiarioSolicitada desde el ASB interno del BC.
// ADR-0024 (decision #8): ProgramacionTurnoDiarioSolicitada es un IPrivateEvent intra-BC y el comando
//   equivalente seria un espejo del evento (mismos campos, sin semantica propia), asi que se consume
//   directo con IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> - sin comando espejo.
// Patron crear-o-actualizar:
//   - CA-3: si NO existe el stream para EmpleadoId+Fecha -> StartStream
//   - CA-4: si YA existe -> GetAggregateRootAsync + AsignarTurno (SaveChanges automatico)
// HU-131 / CA-4: publica DiaCalculado via IPublicEventSender tras el Apply(TurnoDiarioAsignado)
//   que dispara el recalculo reactivo de ControlesDeFranja.
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class ProgramacionTurnoDiarioSolicitadaEventHandler
    : IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada>
{
    private readonly IEventStore _eventStore;
    private readonly IPublicEventSender _publicEventSender;

    public ProgramacionTurnoDiarioSolicitadaEventHandler(
        IEventStore eventStore,
        IPublicEventSender publicEventSender)
    {
        _eventStore = eventStore;
        _publicEventSender = publicEventSender;
    }

    public async Task HandleAsync(ProgramacionTurnoDiarioSolicitada @event, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            @event.Empleado.EmpleadoId, @event.Fecha);

        // CA-ADR-0029 decision #5 (payload por rol): el evento llega con DetalleColaborador/DetalleTurno
        // (PrivateEvents) y TurnoDiarioAsignado persiste ColaboradorProgramado/TurnoDiario
        // (ControlHoras.DomainEvents, propios de este ensamblado desde el issue #322), asi que el
        // mapeo vive aqui -- la Function App es el unico proyecto que ve los tres ensamblados de
        // eventos.
        var evento = new TurnoDiarioAsignado(
            streamId, MapearColaboradorProgramado(@event.Empleado), @event.Fecha,
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

        // HU-131 CA-1/CA-2: tras el Apply(TurnoDiarioAsignado) que dispara el recalculo
        // reactivo de ControlesDeFranja, publica DiaCalculado con la informacion consolidada.
        // Tell-don't-Ask: el aggregate empaqueta el evento via CrearDiaCalculado() (mismo
        // patron que #108). Se emite siempre, incluso si ControlesDeFranja queda vacio o
        // todos son anomalos (CA-2): AsignarTurno/Iniciar siempre agregan el evento al stream.
        await _publicEventSender.PublishAsync(control.CrearDiaCalculado());
    }

    private static ColaboradorProgramado MapearColaboradorProgramado(DetalleColaborador colaborador) =>
        new(colaborador.EmpleadoId, colaborador.TipoIdentificacion, colaborador.NumeroIdentificacion,
            colaborador.Nombres, colaborador.Apellidos);

    // Issue #322: mapeo mecanico DetalleTurno (PrivateEvents) -> TurnoDiario (ControlHoras.DomainEvents),
    // recursivo sobre las franjas y sub-franjas anidadas. Payload por rol (CA-ADR-0029 decision #5).
    private static TurnoDiario MapearTurnoDiario(DetalleTurno turno) =>
        new(turno.Nombre, turno.FranjasOrdinarias.Select(MapearFranja).ToList(), turno.Descripcion);

    // Issue #336: propaga la sede EFECTIVA ya resuelta por la cascada del lado de Programacion
    // (#341) -- mapeo mecanico DetalleSede -> SedeProgramada, tolerante a null (no toda franja de
    // un turno multi-sede trae sede resuelta).
    private static FranjaProgramada MapearFranja(DetalleFranjaOrdinaria franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList(),
            franja.Extras.Select(MapearSubFranja).ToList(),
            franja.Descripcion,
            MapearSede(franja.Sede));

    private static SedeProgramada? MapearSede(DetalleSede? sede) =>
        sede is null ? null : new SedeProgramada(sede.Id, sede.Nombre);

    private static SubFranjaProgramada MapearSubFranja(DetalleSubFranja sub) =>
        new(sub.HoraInicio, sub.HoraFin, sub.DiaOffsetInicio, sub.DiaOffsetFin, sub.Descripcion);
}
