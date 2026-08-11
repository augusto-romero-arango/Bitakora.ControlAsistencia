using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

// Issue #349: handler del comando TerminarVinculacion.
// Flujo esperado (precedente SolicitarProgramacionTurnoCommandHandler, MEF-ADR-0004 capa 2 --
// CA-ADR-0030):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que RegistrarColaboradorCommandHandler).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. colaborador.TerminarVinculacion(command.FechaEfectiva) -- el aggregate declina con
//      resultado (nunca lanza, nunca emite evento de fallo persistido):
//        - ResultadoTerminacionVinculacion.YaTerminada -> throw
//          InvalidOperationException(Mensajes.VinculacionYaTerminada) (-> 409).
//        - ResultadoTerminacionVinculacion.FechaAnteriorAInicio -> throw
//          InvalidOperationException(Mensajes.FechaAnteriorAInicio) (-> 409).
//        - ResultadoTerminacionVinculacion.Exitosa -> el aggregate ya agrego VinculacionTerminada
//          a _uncommittedEvents; WhenAsync/el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #349 "Consumidores: ninguno").
// STUB (fase roja, issue #349): el cuerpo completo queda para el implementer.
public partial class TerminarVinculacionCommandHandler : ICommandHandlerAsync<TerminarVinculacion>
{
    private readonly IEventStore _eventStore;

    public TerminarVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(TerminarVinculacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
