using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;

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
            throw new RecursoNoEncontradoException(Mensajes.SedeNoEncontrada);

        sede.ModificarNombre(command.Nombre);
    }
}
