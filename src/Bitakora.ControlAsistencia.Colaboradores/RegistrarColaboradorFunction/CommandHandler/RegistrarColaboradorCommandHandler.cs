using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

public partial class RegistrarColaboradorCommandHandler : ICommandHandlerAsync<RegistrarColaborador>
{
    private readonly IEventStore _eventStore;

    public RegistrarColaboradorCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RegistrarColaborador command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2): TipoIdentificacion.Desde ya
        // normaliza trim+MAYUSCULAS internamente (issue #371), asi que el borde no repite esa
        // normalizacion. El numero lo normaliza Identificacion.Crear.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var existe = await _eventStore.ExistsAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.ColaboradorYaRegistrado);

        var nombre = NombreColaborador.Crear(
            command.PrimerNombre, command.SegundoNombre, command.PrimerApellido, command.SegundoApellido);

        var colaborador = ColaboradorAggregateRoot.Registrar(
            identificacion, nombre, command.CodigoColaborador, command.FechaInicio, command.CodigoSede);

        _eventStore.StartStream(colaborador);
    }
}
