using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction.CommandHandler;

public partial class PublicarProgramacionCommandHandler : ICommandHandlerAsync<PublicarProgramacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public PublicarProgramacionCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(PublicarProgramacion command, CancellationToken ct = default)
    {
        var ausencias = await _eventStore.GetAggregateRootAsync<AusenciasColaborador>(
            AusenciasColaborador.ComputarStreamId(command.CodigoColaborador), ct);

        var lotes = new List<LotePublicado>();
        foreach (var mes in MesesDelRango(command.Desde, command.Hasta))
        {
            var borrador = await _eventStore.GetAggregateRootAsync<BorradorMensual>(
                BorradorMensual.ComputarStreamId(command.CodigoColaborador, mes), ct);
            if (borrador is null)
                continue;

            lotes.AddRange(borrador.Publicar(command.Desde, command.Hasta, ausencias).Lotes);
        }

        // Un mensaje por solicitud de origen, aunque sus fechas vengan de dos meses.
        var eventos = lotes
            .GroupBy(l => l.SolicitudId)
            .Select(g => (IPrivateEvent)MapearLote(g.First(), [.. g.SelectMany(l => l.Fechas).Order()]))
            .ToArray();
        if (eventos.Length == 0)
            return;

        await _privateEventSender.PublishAsync(
            new PublishOptions { GroupId = command.CodigoColaborador }, eventos);
    }

    private static IEnumerable<DateOnly> MesesDelRango(DateOnly desde, DateOnly hasta)
    {
        for (var mes = new DateOnly(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
            yield return mes;
    }

    private static ProgramacionTurnoEnFechasSolicitada MapearLote(LotePublicado lote, IReadOnlyList<DateOnly> fechas) =>
        new(lote.SolicitudId,
            new ResumenColaborador(lote.Colaborador.Identificacion, lote.Colaborador.CodigoColaborador, lote.Colaborador.NombreCompleto),
            fechas,
            MapearTurno(lote.DetalleTurno),
            DetalleJornadaMapeo.Mapear(lote.Jornada),
            MapearSede(lote.Sede));

    private static DetalleTurno MapearTurno(TurnoProgramado turno) =>
        new(turno.Nombre,
            turno.FranjasOrdinarias.Select(MapearFranja).ToList().AsReadOnly(),
            turno.Descripcion,
            turno.AmplitudVentanaEntradaEnMinutos,
            turno.EsHorarioAbierto);

    private static DetalleFranjaOrdinaria MapearFranja(FranjaProgramada franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Extras.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Descripcion,
            MapearSede(franja.Sede));

    private static DetalleSubFranja MapearSubFranja(SubFranjaProgramada subFranja) =>
        new(subFranja.HoraInicio, subFranja.HoraFin, subFranja.DiaOffsetInicio,
            subFranja.DiaOffsetFin, subFranja.Descripcion);

    private static DetalleSede? MapearSede(SedeProgramada? sede) =>
        sede is null ? null : new DetalleSede(sede.Id, sede.Nombre, sede.CentroDeCostos);

}
