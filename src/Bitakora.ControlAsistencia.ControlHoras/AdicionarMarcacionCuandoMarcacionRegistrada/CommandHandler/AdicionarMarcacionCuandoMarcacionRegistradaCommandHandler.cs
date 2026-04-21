using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.CommandHandler;

// HU-106: Handler de Wolverine que adiciona una marcacion al ControlDiario correspondiente
// Trigger: evento local MarcacionRegistrada publicado via WolverinePrivateEventSender (#105)
// Patron crear-o-actualizar: ExistsAsync -> si no existe StartStream, si existe GetAggregateRootAsync
// CA-9: ventana de traslape nocturno con corte a las 04:00 como constante del handler
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler
    : ICommandHandlerAsync<MarcacionRegistrada>
{
    private readonly IEventStore _eventStore;

    // CA-9: constante del handler - no del aggregate. Cuando sea configurable por empresa
    // vendra de un servicio externo, no de aqui.
    internal static readonly TimeOnly HoraCorteTraslapeNocturno = new TimeOnly(4, 0);

    public AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task HandleAsync(MarcacionRegistrada command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
