using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

// Issue #354: handler del comando AnularTerminacion.
// Flujo esperado (precedente TerminarVinculacionCommandHandler #349, MEF-ADR-0004 capa 2 --
// CA-ADR-0030):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que los handlers hermanos).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. colaborador.AnularTerminacion() -- el aggregate declina con resultado (nunca lanza, nunca
//      emite evento de fallo persistido):
//        - ResultadoAnulacionTerminacion.VinculacionAbierta -> throw
//          InvalidOperationException(Mensajes.VinculacionAbierta) (-> 409).
//        - ResultadoAnulacionTerminacion.Exitosa -> el aggregate ya agrego TerminacionAnulada a
//          _uncommittedEvents; WhenAsync/el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #354 "Consumidores: ninguno").
// STUB (fase roja, issue #354): el cuerpo completo queda para el implementer.
public partial class AnularTerminacionCommandHandler : ICommandHandlerAsync<AnularTerminacion>
{
    private readonly IEventStore _eventStore;

    public AnularTerminacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AnularTerminacion command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // TerminarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.AnularTerminacion();
        if (resultado == ResultadoAnulacionTerminacion.VinculacionAbierta)
            throw new InvalidOperationException(Mensajes.VinculacionAbierta);
    }
}
