using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler;

public partial class AsignarJornadaCommandHandler(IEventStore eventStore) : ICommandHandlerAsync<AsignarJornada>
{
    public async Task HandleAsync(AsignarJornada command, CancellationToken ct = default)
    {
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);
        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.AsignarJornada(command.JornadaId);
        if (resultado == ResultadoAsignacionJornada.VinculacionTerminada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.VinculacionTerminada);
    }
}
