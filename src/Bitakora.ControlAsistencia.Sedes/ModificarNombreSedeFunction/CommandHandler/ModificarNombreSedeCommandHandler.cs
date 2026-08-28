using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;

// Sede inexistente se declina con KeyNotFoundException (el endpoint la traduce a 404), sin
// persistir ningun evento de fallo -- CA-ADR-0030.
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
