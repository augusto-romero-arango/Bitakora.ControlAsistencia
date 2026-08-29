using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler;

// Traduce el resultado declinado del aggregate a InvalidOperationException/409 (CA-ADR-0030).
// Sede inexistente es precondicion de orquestacion (KeyNotFoundException/404), sin evento de fallo
// persistido.
public partial class InstalarDispositivoCommandHandler : ICommandHandlerAsync<InstalarDispositivo>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorUbicacionDispositivo _lector;

    public InstalarDispositivoCommandHandler(IEventStore eventStore, ILectorUbicacionDispositivo lector)
    {
        _eventStore = eventStore;
        _lector = lector;
    }

    public async Task HandleAsync(InstalarDispositivo command, CancellationToken ct = default)
    {
        var streamId = SedeAggregateRoot.ComputarStreamId(command.Codigo);

        // CA-1 (#477): rechazo cross-sede ANTES de cargar el aggregate destino (rechazo barato).
        var ubicacion = await _lector.BuscarUbicacionAsync(command.DispositivoId, ct);
        if (ubicacion is not null && ubicacion.SedeId != streamId)
            throw new InvalidOperationException(Mensajes.DispositivoInstaladoEnOtraSede);

        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new KeyNotFoundException(Mensajes.SedeNoEncontrada);

        var resultado = sede.InstalarDispositivo(command.DispositivoId);
        if (resultado == ResultadoInstalacionDispositivo.YaInstalado)
            throw new InvalidOperationException(Mensajes.DispositivoYaInstalado);
    }
}
