using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

public partial class AnularTerminacionCommandHandler : ICommandHandlerAsync<AnularTerminacion>
{
    private readonly IEventStore _eventStore;

    public AnularTerminacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AnularTerminacion command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // TerminarVinculacionCommandHandler/IniciarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.AnularTerminacion(command.Codigo);
        switch (resultado)
        {
            case ResultadoAnulacionTerminacion.CodigoNoCorresponde:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.CodigoNoCorresponde);
            case ResultadoAnulacionTerminacion.VinculacionAbierta:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.VinculacionAbierta);
        }
    }
}
