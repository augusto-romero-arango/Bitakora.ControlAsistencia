using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;

public partial class IniciarVinculacionCommandHandler : ICommandHandlerAsync<IniciarVinculacion>
{
    private readonly IEventStore _eventStore;

    public IniciarVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(IniciarVinculacion command, CancellationToken ct = default)
    {
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.IniciarVinculacion(
            command.CodigoColaborador, command.FechaInicio, command.CodigoSede);
        switch (resultado)
        {
            case ResultadoInicioVinculacion.VinculacionAbierta:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.VinculacionAbierta);
            case ResultadoInicioVinculacion.FechaSolapaVinculacionAnterior:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.FechaSolapaVinculacionAnterior);
        }
    }
}
