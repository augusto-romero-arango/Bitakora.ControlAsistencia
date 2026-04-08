using Bitakora.ControlAsistencia.Contracts.Eventos;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.CommandHandler;

// HU-12: Handler que asigna el turno diario al ControlDiario cuando llega
//        ProgramacionTurnoDiarioSolicitada desde Service Bus.
// Patron crear-o-actualizar:
//   - CA-3: si NO existe el stream para EmpleadoId+Fecha -> StartStream
//   - CA-4: si YA existe -> GetAggregateRootAsync + AppendEvent
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler
    : ICommandHandlerAsync<ProgramacionTurnoDiarioSolicitada>
{
    private readonly IEventStore _eventStore;

    public AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task HandleAsync(ProgramacionTurnoDiarioSolicitada command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
