using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

public partial class AsignarEtiquetaCommandHandler : ICommandHandlerAsync<AsignarEtiqueta>
{
    private readonly IEventStore _eventStore;

    public AsignarEtiquetaCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarEtiqueta command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // CorregirFechaInicioVinculacionCommandHandler/IniciarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var etiqueta = Etiqueta.Crear(command.Categoria, command.Valor);

        var resultado = colaborador.AsignarEtiqueta(etiqueta);
        if (resultado == ResultadoAsignacionEtiqueta.VinculacionTerminada)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.VinculacionTerminada);
    }
}
