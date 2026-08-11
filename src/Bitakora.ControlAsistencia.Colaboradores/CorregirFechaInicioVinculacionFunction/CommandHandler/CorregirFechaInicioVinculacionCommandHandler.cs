using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

// Issue #352: handler del comando CorregirFechaInicioVinculacion. Combina el flujo de
// ReingresarColaboradorCommandHandler (dos razones de rechazo -> 409) con el de
// CorregirNombresCommandHandler (idempotencia silenciosa, sin excepcion).
// Flujo esperado (MEF-ADR-0004 capa 2 -- CA-ADR-0030):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que los handlers hermanos).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. colaborador.CorregirFechaInicio(command.FechaCorregida) -- el aggregate declina con
//      resultado o en silencio (nunca lanza, nunca emite evento de fallo persistido):
//        - ResultadoCorreccionFechaInicioVinculacion.FechaPosteriorATerminacionPropia -> throw
//          InvalidOperationException(Mensajes.FechaPosteriorATerminacionPropia) (-> 409).
//        - ResultadoCorreccionFechaInicioVinculacion.FechaSolapaVinculacionAnterior -> throw
//          InvalidOperationException(Mensajes.FechaSolapaVinculacionAnterior) (-> 409).
//        - ResultadoCorreccionFechaInicioVinculacion.SinCambios -> no hace nada (idempotencia
//          silenciosa, patron CorregirNombresCommandHandler).
//        - ResultadoCorreccionFechaInicioVinculacion.Exitosa -> el aggregate ya agrego
//          FechaInicioVinculacionCorregida a _uncommittedEvents; WhenAsync/el middleware persiste
//          via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #352 "Consumidores: ninguno").
// STUB (fase roja, issue #352): el cuerpo completo queda para el implementer.
public partial class CorregirFechaInicioVinculacionCommandHandler
    : ICommandHandlerAsync<CorregirFechaInicioVinculacion>
{
    private readonly IEventStore _eventStore;

    public CorregirFechaInicioVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(CorregirFechaInicioVinculacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
