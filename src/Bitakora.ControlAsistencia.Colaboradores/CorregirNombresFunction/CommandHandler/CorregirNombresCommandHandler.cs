using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

// Issue #351: handler del comando CorregirNombres (precedente TerminarVinculacionCommandHandler).
// El mas simple del ciclo de vida: sin caso 409 -- CA-ADR-0030 solo exige el 404 de orquestacion
// (colaborador inexistente, MEF-ADR-0004 capa 2), y el aggregate declina en SILENCIO cuando no hay
// nada que corregir, sin razon que traducir. En el camino de exito el aggregate ya dejo
// NombresCorregidos en _uncommittedEvents -- el middleware persiste via SaveChanges. Sin
// publicacion a bus (event-sourcing puro, "Consumidores: ninguno").
public partial class CorregirNombresCommandHandler : ICommandHandlerAsync<CorregirNombres>
{
    private readonly IEventStore _eventStore;

    public CorregirNombresCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(CorregirNombres command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que los handlers
        // hermanos: TipoIdentificacion.Desde es case-sensitive por diseno (#348), asi que el borde
        // normaliza antes de consultar la lista cerrada.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);
        var nombre = NombreColaborador.Crear(
            command.PrimerNombre, command.SegundoNombre, command.PrimerApellido, command.SegundoApellido);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        colaborador.CorregirNombres(nombre);
    }
}
