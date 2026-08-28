using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler;

// Mecanismo "declinar con resultado" (CA-ADR-0030): una sede ya activa declina sin mutar ni emitir
// (CA-3), y este handler traduce a InvalidOperationException/409. Sede inexistente es precondicion
// de orquestacion (KeyNotFoundException/404).
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
            throw new KeyNotFoundException(Mensajes.SedeNoEncontrada);

        var resultado = sede.Activar();
        if (resultado == ResultadoActivacionSede.YaActiva)
            throw new InvalidOperationException(Mensajes.SedeYaActiva);
    }
}
