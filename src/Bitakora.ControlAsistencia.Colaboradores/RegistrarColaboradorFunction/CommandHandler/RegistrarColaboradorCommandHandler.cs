using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

// Issue #330: handler del comando RegistrarColaborador (precedente CrearTurnoCommandHandler).
// MEF-ADR-0004 capa 2: comando de creacion sobre stream existente -> InvalidOperationException con
// mensaje .resx, que el FunctionEndpoint traduce a 409 Conflict. Ningun evento de fallo persistido:
// contaminaria el stream del colaborador legitimo.
// Sin publicacion a bus (event-sourcing puro, issue #330 "Consumidores: ninguno").
public partial class RegistrarColaboradorCommandHandler : ICommandHandlerAsync<RegistrarColaborador>
{
    private readonly IEventStore _eventStore;

    public RegistrarColaboradorCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RegistrarColaborador command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2): normalizar trim+MAYUSCULAS ANTES
        // de Desde -- TipoIdentificacion.Desde es case-sensitive por diseno (#348) para proteger la
        // rehidratacion de lo ya persistido; normalizar la ENTRADA del usuario es responsabilidad de
        // este borde, no del VO. El numero lo normaliza Identificacion.Crear.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var existe = await _eventStore.ExistsAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (existe)
            throw new InvalidOperationException(Mensajes.ColaboradorYaRegistrado);

        var nombre = NombreColaborador.Crear(
            command.PrimerNombre, command.SegundoNombre, command.PrimerApellido, command.SegundoApellido);

        var colaborador = ColaboradorAggregateRoot.Registrar(
            identificacion, nombre, command.CodigoColaborador, command.FechaInicio);

        _eventStore.StartStream(colaborador);
    }
}
