using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
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
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
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
