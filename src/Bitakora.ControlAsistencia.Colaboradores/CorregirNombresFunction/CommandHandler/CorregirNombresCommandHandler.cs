using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

// Sin caso 409: el aggregate declina en silencio cuando no hay nada que corregir.
public partial class CorregirNombresCommandHandler : ICommandHandlerAsync<CorregirNombres>
{
    private readonly IEventStore _eventStore;

    public CorregirNombresCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(CorregirNombres command, CancellationToken ct = default)
    {
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);
        var nombre = NombreColaborador.Crear(
            command.PrimerNombre, command.SegundoNombre, command.PrimerApellido, command.SegundoApellido);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        colaborador.CorregirNombres(nombre);
    }
}
