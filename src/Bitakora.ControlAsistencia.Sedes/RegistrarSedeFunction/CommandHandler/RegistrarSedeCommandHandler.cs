using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;

public partial class RegistrarSedeCommandHandler : ICommandHandlerAsync<RegistrarSede>
{
    private readonly IEventStore _eventStore;

    public RegistrarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RegistrarSede command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);
        var existe = await _eventStore.ExistsAsync<SedeAggregateRoot>(streamId, ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.SedeYaRegistrada);

        var sede = SedeAggregateRoot.Registrar(
            command.Codigo, command.Nombre, command.Ciudad, command.Direccion);

        _eventStore.StartStream(sede);
    }
}
