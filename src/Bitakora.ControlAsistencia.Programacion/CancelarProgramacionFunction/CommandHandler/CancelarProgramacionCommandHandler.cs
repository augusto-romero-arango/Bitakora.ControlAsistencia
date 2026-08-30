using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;

public partial class CancelarProgramacionCommandHandler
    : ICommandHandlerAsync<CancelarProgramacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public CancelarProgramacionCommandHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(CancelarProgramacion command, CancellationToken ct = default)
    {
        var solicitudExiste = await _eventStore.ExistsAsync<SolicitudCancelacionAggregateRoot>(
            command.Id, ct);
        if (solicitudExiste)
            throw new InvalidOperationException(Mensajes.SolicitudYaExiste);

        var colaboradorDominio = MapearColaboradorProgramado(command.Colaborador);
        var fechas = command.Fechas.AsReadOnly();

        var evento = new CancelacionProgramacionSolicitada(command.Id, colaboradorDominio, fechas);
        var solicitud = SolicitudCancelacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        var colaborador = MapearResumenColaborador(command.Colaborador);
        var eventosPrivados = command.Fechas
            .Select(fecha => new CancelacionTurnoDiarioSolicitada(command.Id, colaborador, fecha))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
    }

    private static ResumenColaborador MapearResumenColaborador(ColaboradorSolicitado colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    private static ColaboradorProgramado MapearColaboradorProgramado(ColaboradorSolicitado colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);
}
