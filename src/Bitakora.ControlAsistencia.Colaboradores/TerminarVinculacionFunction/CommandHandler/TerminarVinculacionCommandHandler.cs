using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

public partial class TerminarVinculacionCommandHandler : ICommandHandlerAsync<TerminarVinculacion>
{
    private readonly IEventStore _eventStore;

    public TerminarVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(TerminarVinculacion command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // IniciarVinculacionCommandHandler/RegistrarColaboradorCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new RecursoNoEncontradoException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.TerminarVinculacion(command.Codigo, command.FechaEfectiva);
        switch (resultado)
        {
            case ResultadoTerminacionVinculacion.CodigoNoCorresponde:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.CodigoNoCorresponde);
            case ResultadoTerminacionVinculacion.YaTerminada:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.VinculacionYaTerminada);
            case ResultadoTerminacionVinculacion.FechaAnteriorAInicio:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.FechaAnteriorAInicio);
        }
    }
}
