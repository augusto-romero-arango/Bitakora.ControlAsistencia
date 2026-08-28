using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;

// Issue #457: handler del comando ModificarNombreSede (precedente CorregirNombresCommandHandler).
// CA-ADR-0030 / MEF-ADR-0004 capa 2: sede inexistente -> 404 via KeyNotFoundException con mensaje
// .resx. Sin caso 409: este comando no tiene reglas de estado. Sin publicacion a bus (Consumidores:
// ninguno en este issue). Fase roja: stub minimo, el implementer completa.
public partial class ModificarNombreSedeCommandHandler : ICommandHandlerAsync<ModificarNombreSede>
{
    private readonly IEventStore _eventStore;

    public ModificarNombreSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(ModificarNombreSede command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
