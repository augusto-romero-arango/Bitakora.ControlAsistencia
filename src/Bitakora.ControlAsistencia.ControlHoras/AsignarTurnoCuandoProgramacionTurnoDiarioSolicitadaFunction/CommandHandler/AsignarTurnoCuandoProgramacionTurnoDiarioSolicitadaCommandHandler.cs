using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.CommandHandler;

// HU-12: Handler que asigna el turno diario al ControlDiario cuando llega
//        ProgramacionTurnoDiarioSolicitada desde Service Bus.
// Patron crear-o-actualizar:
//   - CA-3: si NO existe el stream para EmpleadoId+Fecha -> StartStream
//   - CA-4: si YA existe -> GetAggregateRootAsync + AsignarTurno (SaveChanges automatico)
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler
    : ICommandHandlerAsync<ProgramacionTurnoDiarioSolicitada>
{
    private readonly IEventStore _eventStore;

    public AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task HandleAsync(ProgramacionTurnoDiarioSolicitada command, CancellationToken ct = default)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(
            command.Empleado.EmpleadoId, command.Fecha);

        var evento = new TurnoDiarioAsignado(
            streamId, command.Empleado, command.Fecha, command.DetalleTurno, command.SolicitudId);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);

        if (!existe)
        {
            var control = ControlDiarioAggregateRoot.Iniciar(evento);
            _eventStore.StartStream(control);
        }
        else
        {
            var control = await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct);
            control!.AsignarTurno(evento);
        }
    }
}
