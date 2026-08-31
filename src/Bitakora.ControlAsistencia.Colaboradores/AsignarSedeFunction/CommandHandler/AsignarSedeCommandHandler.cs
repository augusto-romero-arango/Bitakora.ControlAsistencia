using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler;

// CA-ADR-0030: solo VinculacionTerminada se traduce a error (409). SinCambios NO es un rechazo --
// el borde responde 202 igual, sin evento nuevo. El 404 es precondicion de orquestacion
// (MEF-ADR-0004 capa 2), no regla del aggregate.
// El camino de exito no persiste aqui: el aggregate deja SedeAsignada en _uncommittedEvents y el
// middleware hace SaveChanges.
public partial class AsignarSedeCommandHandler : ICommandHandlerAsync<AsignarSede>
{
    private readonly IEventStore _eventStore;

    public AsignarSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarSede command, CancellationToken ct = default)
    {
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.AsignarSede(command.CodigoSede);
        if (resultado == ResultadoAsignacionSede.VinculacionTerminada)
            throw new InvalidOperationException(Mensajes.VinculacionTerminada);
    }
}
