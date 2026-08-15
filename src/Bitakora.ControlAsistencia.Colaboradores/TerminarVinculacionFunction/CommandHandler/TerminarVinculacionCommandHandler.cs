using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
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
public partial class TerminarVinculacionCommandHandler : ICommandHandlerAsync<TerminarVinculacion>
{
    private readonly IEventStore _eventStore;

    public TerminarVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(TerminarVinculacion command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // IniciarVinculacionCommandHandler/RegistrarColaboradorCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.TerminarVinculacion(command.Codigo, command.FechaEfectiva);
        switch (resultado)
        {
            case ResultadoTerminacionVinculacion.CodigoNoCorresponde:
                throw new InvalidOperationException(Mensajes.CodigoNoCorresponde);
            case ResultadoTerminacionVinculacion.YaTerminada:
                throw new InvalidOperationException(Mensajes.VinculacionYaTerminada);
            case ResultadoTerminacionVinculacion.FechaAnteriorAInicio:
                throw new InvalidOperationException(Mensajes.FechaAnteriorAInicio);
        }
    }
}
