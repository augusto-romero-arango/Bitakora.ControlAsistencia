using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

public partial class RetirarEtiquetaCommandHandler : ICommandHandlerAsync<RetirarEtiqueta>
{
    private readonly IEventStore _eventStore;

    public RetirarEtiquetaCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RetirarEtiqueta command, CancellationToken ct = default)
    {
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var categoriaNormalizada = Etiqueta.NormalizarCategoria(command.Categoria);

        var resultado = colaborador.RetirarEtiqueta(categoriaNormalizada);
        if (resultado == ResultadoRetiroEtiqueta.VinculacionTerminada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.VinculacionTerminada);
    }
}
