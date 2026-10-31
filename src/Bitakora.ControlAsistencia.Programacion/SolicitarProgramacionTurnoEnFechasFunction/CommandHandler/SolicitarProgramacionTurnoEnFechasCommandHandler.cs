using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventDriven.Abstractions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction.CommandHandler;

public partial class SolicitarProgramacionTurnoEnFechasCommandHandler
    : ICommandHandlerAsync<SolicitarProgramacionTurnoEnFechas, ResultadoSolicitudProgramacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;
    private readonly IAseguradorJornadaPredeterminada _asegurador;

    public SolicitarProgramacionTurnoEnFechasCommandHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender,
        IAseguradorJornadaPredeterminada asegurador)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
        _asegurador = asegurador;
    }

    public Task<ResultadoSolicitudProgramacion> HandleAsync(
        SolicitarProgramacionTurnoEnFechas command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
