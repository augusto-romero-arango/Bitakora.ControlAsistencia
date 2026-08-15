using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

// Issue #349: handler del comando TerminarVinculacion.
// Issue #379 (MEF-ADR-0043 paso 4, CA-5): el comando gana el campo Codigo -- el handler debe
// pasarlo a ColaboradorAggregateRoot.TerminarVinculacion(codigo, fechaEfectiva) y traducir el
// nuevo caso ResultadoTerminacionVinculacion.CodigoNoCorresponde a
// InvalidOperationException(Mensajes.CodigoNoCorresponde) (-> 409), igual que los dos casos ya
// existentes (YaTerminada/FechaAnteriorAInicio).
// Flujo esperado (precedente SolicitarProgramacionTurnoCommandHandler, MEF-ADR-0004 capa 2 --
// CA-ADR-0030):
//   1. Parsear TipoIdentificacion -> TipoIdentificacion.Desde(...), que normaliza trim+MAYUSCULAS
//      internamente (issue #371, mismo criterio que RegistrarColaboradorCommandHandler).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. colaborador.TerminarVinculacion(command.Codigo, command.FechaEfectiva) -- el aggregate
//      declina con resultado (nunca lanza, nunca emite evento de fallo persistido):
//        - ResultadoTerminacionVinculacion.CodigoNoCorresponde -> throw
//          InvalidOperationException(Mensajes.CodigoNoCorresponde) (-> 409, evaluada PRIMERA).
//        - ResultadoTerminacionVinculacion.YaTerminada -> throw
//          InvalidOperationException(Mensajes.VinculacionYaTerminada) (-> 409).
//        - ResultadoTerminacionVinculacion.FechaAnteriorAInicio -> throw
//          InvalidOperationException(Mensajes.FechaAnteriorAInicio) (-> 409).
//        - ResultadoTerminacionVinculacion.Exitosa -> el aggregate ya agrego VinculacionTerminada
//          a _uncommittedEvents; WhenAsync/el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #349 "Consumidores: ninguno").
// STUB (fase roja, issue #379): el cuerpo completo queda para el implementer -- este agente nunca
// escribe implementacion real.
public partial class TerminarVinculacionCommandHandler : ICommandHandlerAsync<TerminarVinculacion>
{
    private readonly IEventStore _eventStore;

    public TerminarVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(TerminarVinculacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
