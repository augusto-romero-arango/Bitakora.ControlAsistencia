using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

public partial class SolicitarProgramacionTurnoCommandHandler
    : ICommandHandlerAsync<SolicitarProgramacionTurno>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public SolicitarProgramacionTurnoCommandHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
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

        var detalleTurno = catalogo.ObtenerDetalle();
        var fechas = command.Fechas.AsReadOnly();

        var evento = new ProgramacionTurnoSolicitada(command.Id, command.Empleado, fechas, detalleTurno);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        // ADR-0024 decision #2/#3: ProgramacionTurnoDiarioSolicitada es intra-BC (lo consume
        // ControlHoras, mismo BC) -> IPrivateEvent publicado al ASB interno via IPrivateEventSender.
        var eventosPrivados = command.Fechas
            .Select(fecha => new ProgramacionTurnoDiarioSolicitada(
                command.Id, command.Empleado, fecha, detalleTurno))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
    }
}
