using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
// Esta isla declara su propio ResumenColaborador (payload de TurnoDiarioCancelado -> Colaborador),
// homonimo del que trae el bus: el alias fija cual de los dos entra por el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaFunction.EventHandler;

// Issue #499, lado ControlHoras de "Cancelar Programacion" (#498). El evento privado es espejo
// directo del hecho a registrar, asi que se consume sin comando intermedio (MEF-ADR-0024
// decision #8), mismo criterio que ProgramacionTurnoDiarioSolicitadaEventHandler.
// A diferencia de aquel, el ramal "stream no existe" NO inicia stream: es no-op silencioso
// (sin evento de constancia -- la auditoria del acto quedo en Programacion).
// partial para admitir una clase Mensajes en archivo separado (MEF-ADR-0009), si llega a hacer falta.
public partial class CancelacionTurnoDiarioSolicitadaEventHandler
    : IPrivateEventHandlerAsync<CancelacionTurnoDiarioSolicitada>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public CancelacionTurnoDiarioSolicitadaEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(CancelacionTurnoDiarioSolicitada @event, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            @event.Colaborador.CodigoColaborador, @event.Fecha);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);
        if (!existe) return;

        var control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;

        // El evento llega con los tipos de PrivateEvents y TurnoDiarioCancelado persiste los de
        // ControlHoras.DomainEvents. El mapeo vive aqui porque esta Function App es el unico
        // proyecto que ve las tres islas de eventos (CA-ADR-0029 decision #5, payload por rol).
        // Crear() en vez de new: el ctor parametrizado es internal (solo ControlHoras.Tests tiene
        // InternalsVisibleTo sobre este ensamblado), asi que este Function App usa la puerta publica.
        var evento = TurnoDiarioCancelado.Crear(
            streamId, MapearColaboradorProgramado(@event.Colaborador), @event.Fecha, @event.SolicitudId);

        var resultado = control.CancelarTurno(evento);
        if (resultado == ResultadoCancelacionTurno.SinTurnoAsignado) return;

        // CrearDiaDepurado() debe invocarse DESPUES del Apply: lee el desglose que el Apply
        // recalcula.
        await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }

    private static ColaboradorProgramado MapearColaboradorProgramado(ResumenColaborador colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);
}
