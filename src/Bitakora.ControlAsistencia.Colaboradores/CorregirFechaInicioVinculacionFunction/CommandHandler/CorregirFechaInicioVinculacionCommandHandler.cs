using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

// Issue #352: handler del comando CorregirFechaInicioVinculacion. Combina los dos mecanismos que
// el ciclo de vida ya usa (CA-ADR-0030): el aggregate declina CON RESULTADO las dos reglas de
// estado -- que este handler traduce a InvalidOperationException/409 con mensaje .resx -- y declina
// EN SILENCIO la idempotencia (SinCambios), que no es un rechazo: el borde responde 202 igual, como
// en CorregirNombresCommandHandler. El 404 es precondicion de orquestacion (MEF-ADR-0004 capa 2),
// no regla del aggregate. En el camino de exito el aggregate ya dejo FechaInicioVinculacionCorregida
// en _uncommittedEvents -- el middleware persiste via SaveChanges. Sin publicacion a bus
// (event-sourcing puro, issue #352 "Consumidores: ninguno").
// Issue #379 (MEF-ADR-0043 paso 4, CA-5): el comando gana el campo Codigo -- el handler debe
// pasarlo a ColaboradorAggregateRoot.CorregirFechaInicio(codigo, fechaCorregida) y traducir el
// nuevo caso ResultadoCorreccionFechaInicioVinculacion.CodigoNoCorresponde a
// InvalidOperationException(Mensajes.CodigoNoCorresponde) (-> 409), evaluada ANTES que SinCambios
// y las demas reglas.
// STUB (fase roja, issue #379): el cuerpo completo queda para el implementer -- este agente nunca
// escribe implementacion real.
public partial class CorregirFechaInicioVinculacionCommandHandler
    : ICommandHandlerAsync<CorregirFechaInicioVinculacion>
{
    private readonly IEventStore _eventStore;

    public CorregirFechaInicioVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(CorregirFechaInicioVinculacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
