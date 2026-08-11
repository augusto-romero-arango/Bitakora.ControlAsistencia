using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
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
public partial class CorregirFechaInicioVinculacionCommandHandler
    : ICommandHandlerAsync<CorregirFechaInicioVinculacion>
{
    private readonly IEventStore _eventStore;

    public CorregirFechaInicioVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(CorregirFechaInicioVinculacion command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // ReingresarColaboradorCommandHandler/TerminarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.CorregirFechaInicio(command.FechaCorregida);
        switch (resultado)
        {
            case ResultadoCorreccionFechaInicioVinculacion.FechaPosteriorATerminacionPropia:
                throw new InvalidOperationException(Mensajes.FechaPosteriorATerminacionPropia);
            case ResultadoCorreccionFechaInicioVinculacion.FechaSolapaVinculacionAnterior:
                throw new InvalidOperationException(Mensajes.FechaSolapaVinculacionAnterior);
        }
    }
}
