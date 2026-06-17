using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.CommandHandler;

// HU-12: Handler que asigna el turno diario al ControlDiario cuando llega
//        ProgramacionTurnoDiarioSolicitada desde Service Bus.
// Patron crear-o-actualizar:
//   - CA-3: si NO existe el stream para EmpleadoId+Fecha -> StartStream
//   - CA-4: si YA existe -> GetAggregateRootAsync + AsignarTurno (SaveChanges automatico)
// HU-131: publica DiaCalculado via IPublicEventSender tras el Apply(TurnoDiarioAsignado)
//         que dispara el recalculo reactivo de ControlesDeFranja.
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler
    : ICommandHandlerAsync<ProgramacionTurnoDiarioSolicitada>
{
    private readonly IEventStore _eventStore;
    private readonly IPublicEventSender _publicEventSender;

    public AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler(
        IEventStore eventStore,
        IPublicEventSender publicEventSender)
    {
        _eventStore = eventStore;
        _publicEventSender = publicEventSender;
    }

    public async Task HandleAsync(ProgramacionTurnoDiarioSolicitada command, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            command.Empleado.EmpleadoId, command.Fecha);

        var evento = new TurnoDiarioAsignado(
            streamId, command.Empleado, command.Fecha, command.DetalleTurno, command.SolicitudId);

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
}
