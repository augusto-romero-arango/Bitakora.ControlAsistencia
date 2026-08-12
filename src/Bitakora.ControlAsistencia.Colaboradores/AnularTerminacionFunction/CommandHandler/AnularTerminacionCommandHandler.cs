using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

// Issue #354: handler del comando AnularTerminacion. Usa el mecanismo "declinar con resultado"
// (CA-ADR-0030) con una unica razon de rechazo -- la ultima vinculacion no tiene terminacion
// registrada --, que este handler traduce a InvalidOperationException/409 con mensaje .resx. El 404
// es precondicion de orquestacion (MEF-ADR-0004 capa 2), no regla del aggregate. En el camino de
// exito el aggregate ya dejo TerminacionAnulada en _uncommittedEvents -- el middleware persiste via
// SaveChanges. Sin publicacion a bus (event-sourcing puro, issue #354 "Consumidores: ninguno").
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
