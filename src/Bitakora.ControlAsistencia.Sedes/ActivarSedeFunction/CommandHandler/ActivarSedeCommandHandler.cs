using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler;

public partial class ActivarSedeCommandHandler : ICommandHandlerAsync<ActivarSede>
{
    private readonly IEventStore _eventStore;

    public ActivarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(ActivarSede command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new RecursoNoEncontradoException(Mensajes.SedeNoEncontrada);

        var resultado = sede.Activar();
        if (resultado == ResultadoActivacionSede.YaActiva)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.SedeYaActiva);
    }
}
