using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
// Esta isla declara su propio ResumenColaborador (payload de TurnoDiarioCancelado -> Colaborador),
// homonimo del que trae el bus: el alias fija cual de los dos entra por el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada.EventHandler;

// A diferencia del handler de asignacion, si el stream no existe no se inicia: es no-op silencioso,
// sin evento de constancia, porque la auditoria del acto quedo en Programacion.
public class CancelacionTurnoDiarioSolicitadaEventHandler
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

        // El mapeo PrivateEvents -> DomainEvents vive aqui porque esta Function App es el unico
        // proyecto que ve las islas de eventos (CA-ADR-0029 decision #5).
        var evento = TurnoDiarioCancelado.Crear(
            streamId, MapearColaboradorProgramado(@event.Colaborador), @event.Fecha, @event.SolicitudId);

        var resultado = control.CancelarTurno(evento);
        if (resultado == ResultadoCancelacionTurno.SinTurnoAsignado) return;

        // CrearDiaDepurado() debe invocarse DESPUES del Apply: lee el desglose que el Apply
        // recalcula.
        var diaDepurado = control.CrearDiaDepurado();
        await _privateEventSender.PublishAsync(new PublishOptions { GroupId = diaDepurado.CodigoColaborador }, diaDepurado);
    }

    private static ColaboradorProgramado MapearColaboradorProgramado(ResumenColaborador colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);
}
