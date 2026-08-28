using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler;

// Mecanismo "declinar con resultado" (CA-ADR-0030): una sede ya activa declina sin mutar ni emitir
// (CA-3), y este handler traduce a InvalidOperationException/409. Sede inexistente es precondicion
// de orquestacion (KeyNotFoundException/404). Fase roja: stub minimo, el implementer completa la
// orquestacion real.
public partial class ActivarSedeCommandHandler : ICommandHandlerAsync<ActivarSede>
{
    private readonly IEventStore _eventStore;

    public ActivarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(ActivarSede command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
