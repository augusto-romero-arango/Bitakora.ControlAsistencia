using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

// Issue #355: handler del comando AsignarEtiqueta. Combina los dos mecanismos que el ciclo de vida
// ya usa (CA-ADR-0030, precedente CorregirFechaInicioVinculacionCommandHandler #352): el aggregate
// declina CON RESULTADO la regla de apertura estricta (VinculacionTerminada), que este handler
// traduce a InvalidOperationException/409 con mensaje .resx, y declina EN SILENCIO la idempotencia
// (SinCambios), que no es un rechazo: el borde responde 202 igual, como en
// CorregirNombresCommandHandler. El 404 es precondicion de orquestacion (MEF-ADR-0004 capa 2), no
// regla del aggregate. En el camino de exito el aggregate ya dejo EtiquetaAsignada en
// _uncommittedEvents -- el middleware persiste via SaveChanges. Sin publicacion a bus
// (event-sourcing puro, issue #355 "Consumidores: ninguno").
public partial class AsignarEtiquetaCommandHandler : ICommandHandlerAsync<AsignarEtiqueta>
{
    private readonly IEventStore _eventStore;

    public AsignarEtiquetaCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarEtiqueta command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // CorregirFechaInicioVinculacionCommandHandler/IniciarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var etiqueta = Etiqueta.Crear(command.Categoria, command.Valor);

        var resultado = colaborador.AsignarEtiqueta(etiqueta);
        if (resultado == ResultadoAsignacionEtiqueta.VinculacionTerminada)
            throw new InvalidOperationException(Mensajes.VinculacionTerminada);
    }
}
