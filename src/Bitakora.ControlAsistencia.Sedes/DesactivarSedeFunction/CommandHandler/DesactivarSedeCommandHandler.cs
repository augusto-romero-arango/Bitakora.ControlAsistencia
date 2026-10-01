using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler;

public partial class DesactivarSedeCommandHandler : ICommandHandlerAsync<DesactivarSede>
{
    private readonly IEventStore _eventStore;

    public DesactivarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(DesactivarSede command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new RecursoNoEncontradoException(Mensajes.SedeNoEncontrada);

        var resultado = sede.Desactivar();
        if (resultado == ResultadoDesactivacionSede.YaInactiva)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.SedeYaInactiva);
    }
}
