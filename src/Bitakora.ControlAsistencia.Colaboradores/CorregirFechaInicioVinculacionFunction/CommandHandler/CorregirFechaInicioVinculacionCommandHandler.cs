using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

public partial class CorregirFechaInicioVinculacionCommandHandler
    : ICommandHandlerAsync<CorregirFechaInicioVinculacion>
{
    private readonly IEventStore _eventStore;

    public CorregirFechaInicioVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(CorregirFechaInicioVinculacion command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // TerminarVinculacionCommandHandler/IniciarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.CorregirFechaInicio(command.Codigo, command.FechaCorregida);
        switch (resultado)
        {
            case ResultadoCorreccionFechaInicioVinculacion.CodigoNoCorresponde:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.CodigoNoCorresponde);
            case ResultadoCorreccionFechaInicioVinculacion.FechaPosteriorATerminacionPropia:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.FechaPosteriorATerminacionPropia);
            case ResultadoCorreccionFechaInicioVinculacion.FechaSolapaVinculacionAnterior:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.FechaSolapaVinculacionAnterior);
        }
    }
}
