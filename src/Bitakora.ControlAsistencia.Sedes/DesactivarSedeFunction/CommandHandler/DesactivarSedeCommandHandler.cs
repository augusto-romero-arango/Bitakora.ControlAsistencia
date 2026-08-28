using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler;

// Mecanismo "declinar con resultado" (CA-ADR-0030): una sede ya inactiva declina sin mutar ni
// emitir (CA-4), y este handler traduce a InvalidOperationException/409. Sede inexistente es
// precondicion de orquestacion (KeyNotFoundException/404). Fase roja: stub minimo, el implementer
// completa la orquestacion real.
public partial class DesactivarSedeCommandHandler : ICommandHandlerAsync<DesactivarSede>
{
    private readonly IEventStore _eventStore;

    public DesactivarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(DesactivarSede command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
