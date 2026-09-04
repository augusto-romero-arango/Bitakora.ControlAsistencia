using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler;

// El handler construye FranjaOrdinaria (invariantes del VO) ANTES de leer el aggregate: una
// ArgumentException del factory sube sin tocar el catalogo (CA-ADR-0030 -- dos canales de error
// distintos, nunca mezclados en el mismo metodo del aggregate).
public partial class AgregarFranjaCommandHandler : ICommandHandlerAsync<AgregarFranja>
{
    private readonly IEventStore _eventStore;

    public AgregarFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(AgregarFranja command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
