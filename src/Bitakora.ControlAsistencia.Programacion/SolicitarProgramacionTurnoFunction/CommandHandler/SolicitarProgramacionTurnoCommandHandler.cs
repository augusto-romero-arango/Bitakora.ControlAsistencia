using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

public partial class SolicitarProgramacionTurnoCommandHandler
    : ICommandHandlerAsync<SolicitarProgramacionTurno>
{
    private readonly IEventStore _eventStore;
    private readonly IPublicEventSender _publicEventSender;

    public SolicitarProgramacionTurnoCommandHandler(
        IEventStore eventStore,
        IPublicEventSender publicEventSender)
    {
        _eventStore = eventStore;
        _publicEventSender = publicEventSender;
    }

    public async Task HandleAsync(SolicitarProgramacionTurno command, CancellationToken ct = default)
    {
        var solicitudExiste = await _eventStore.ExistsAsync<SolicitudProgramacionAggregateRoot>(
            command.Id, ct);
        if (solicitudExiste)
            throw new InvalidOperationException(Mensajes.SolicitudYaExiste);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        var empleado = new InformacionEmpleado(
            command.Empleado.EmpleadoId,
            command.Empleado.TipoIdentificacion,
            command.Empleado.NumeroIdentificacion,
            command.Empleado.Nombres,
            command.Empleado.Apellidos);

        var detalleTurno = catalogo.ObtenerDetalle();
        var fechas = command.Fechas.AsReadOnly();

        var evento = new ProgramacionTurnoSolicitada(command.Id, empleado, fechas, detalleTurno);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        var eventosPublicos = command.Fechas
            .Select(fecha => new ProgramacionTurnoDiarioSolicitada(
                command.Id, empleado, fecha, detalleTurno))
            .Cast<IPublicEvent>()
            .ToArray();

        await _publicEventSender.PublishAsync(eventosPublicos);
    }
}
