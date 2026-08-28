using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;

// Issue #456: handler del comando RegistrarSede (precedente RegistrarColaboradorCommandHandler).
// CA-ADR-0030 / MEF-ADR-0004 capa 2: comando de creacion sobre stream existente -> 409 Conflict via
// InvalidOperationException con mensaje .resx. Ningun evento de fallo persistido: contaminaria el
// stream de la sede legitima. Sin publicacion a bus (SedeRegistrada no cruza el bus en este issue).
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
            throw new InvalidOperationException(Mensajes.SedeYaRegistrada);

        var sede = SedeAggregateRoot.Registrar(
            command.Codigo, command.Nombre, command.Ciudad, command.Direccion);

        _eventStore.StartStream(sede);
    }
}
