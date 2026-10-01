using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler;

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

        // El rechazo cross-sede va ANTES de cargar el aggregate destino: rechazo barato, y el
        // 409 prevalece aunque la sede destino no exista.
        var ubicacion = await _lector.BuscarUbicacionAsync(command.DispositivoId, ct);
        if (ubicacion is not null && ubicacion.SedeId != streamId)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.DispositivoInstaladoEnOtraSede);

        var sede = await _eventStore.GetAggregateRootAsync<SedeAggregateRoot>(streamId, ct);
        if (sede is null)
            throw new RecursoNoEncontradoException(Mensajes.SedeNoEncontrada);

        var resultado = sede.InstalarDispositivo(command.DispositivoId);
        if (resultado == ResultadoInstalacionDispositivo.YaInstalado)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.DispositivoYaInstalado);
    }
}
