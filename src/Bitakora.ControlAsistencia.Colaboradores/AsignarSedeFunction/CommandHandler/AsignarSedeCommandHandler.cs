using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler;

// Issue #465: handler del comando AsignarSede. Combina los dos mecanismos que el ciclo de vida ya
// usa (CA-ADR-0030, precedente exacto AsignarEtiquetaCommandHandler #355): el aggregate declina CON
// RESULTADO la regla de apertura estricta (VinculacionTerminada), que este handler traduce a
// InvalidOperationException/409 con mensaje .resx, y declina EN SILENCIO la idempotencia
// (SinCambios), que no es un rechazo: el borde responde 202 igual. El 404 es precondicion de
// orquestacion (MEF-ADR-0004 capa 2), no regla del aggregate. En el camino de exito el aggregate ya
// dejo SedeAsignada en _uncommittedEvents -- el middleware persiste via SaveChanges. Sin
// publicacion a bus (event-sourcing puro, issue #465 "Consumidores: ninguno").
public partial class AsignarSedeCommandHandler : ICommandHandlerAsync<AsignarSede>
{
    private readonly IEventStore _eventStore;

    public AsignarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarSede command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // AsignarEtiquetaCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.AsignarSede(command.CodigoSede);
        if (resultado == ResultadoAsignacionSede.VinculacionTerminada)
            throw new InvalidOperationException(Mensajes.VinculacionTerminada);
    }
}
