using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;

// Issue #457: handler del comando ModificarNombreSede (precedente CorregirNombresCommandHandler).
// CA-ADR-0030 / MEF-ADR-0004 capa 2: sede inexistente -> 404 via KeyNotFoundException con mensaje
// .resx. Sin caso 409: este comando no tiene reglas de estado. Sin publicacion a bus (Consumidores:
// ninguno en este issue).
public partial class ModificarNombreSedeCommandHandler : ICommandHandlerAsync<ModificarNombreSede>
{
    private readonly IEventStore _eventStore;

    public ModificarNombreSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(ModificarNombreSede command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new KeyNotFoundException(Mensajes.SedeNoEncontrada);

        sede.ModificarNombre(command.Nombre);
    }
}
