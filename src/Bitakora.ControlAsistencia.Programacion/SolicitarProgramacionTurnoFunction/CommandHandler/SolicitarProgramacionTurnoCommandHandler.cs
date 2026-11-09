using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

// Reparte las fechas libres entre dias en firme (a ControlHoras) y borrador mensual (CA-ADR-0045).
public partial class SolicitarProgramacionTurnoCommandHandler
    : ICommandHandlerAsync<SolicitarProgramacionTurno, ResultadoSolicitudProgramacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;
    private readonly IAseguradorJornadaPredeterminada _asegurador;

    public SolicitarProgramacionTurnoCommandHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender,
        IAseguradorJornadaPredeterminada asegurador)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
        _asegurador = asegurador;
    }

    public async Task<ResultadoSolicitudProgramacion> HandleAsync(SolicitarProgramacionTurno command, CancellationToken ct = default)
    {
        var solicitudExiste = await _eventStore.ExistsAsync<SolicitudProgramacionAggregateRoot>(
            command.Id, ct);
        if (solicitudExiste)
            throw new RecursoYaExisteException(Mensajes.SolicitudYaExiste);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        // Guarda transaccional contra el aggregate ya cargado, no contra una vista (CA-ADR-0033).
        switch (catalogo.EvaluarAsignabilidad())
        {
            case ResultadoAsignabilidadTurno.Retirado:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoRetirado);
            case ResultadoAsignabilidadTurno.Incompleto:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoIncompleto);
        }

        // Unico punto de normalizacion: aguas abajo solo viaja sedeSolicitada, nunca command.Sede.
        var sedeSolicitada = command.Sede?.ConCentroDeCostosNormalizado();

        // La cascada va antes de construir los eventos: ambos payloads derivan del mismo turno
        // resuelto. Con sede null es identidad, sin ramificar (MEF-ADR-0012).
        var turnoProgramado = catalogo.ObtenerDetalle().ConSedePorDefecto(sedeSolicitada);

        var jornadaId = command.JornadaId ?? await _asegurador.AsegurarAsync(ct);
        var jornada = await _eventStore.GetAggregateRootAsync<Jornada>(jornadaId, ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);
        var jornadaProgramada = jornada.Estampar();

        var ausencias = await _eventStore.GetAggregateRootAsync<AusenciasColaborador>(
            AusenciasColaborador.ComputarStreamId(command.Colaborador.CodigoColaborador), ct);
        var clasificacion = ausencias?.ClasificarFechas(command.Fechas)
            ?? new ClasificacionFechas(command.Fechas, []);
        if (clasificacion.Libres.Count == 0)
        {
            var detalle = string.Join(", ", clasificacion.ConAusencia.Select(f => $"{f.Fecha:yyyy-MM-dd} ({f.Motivo})"));
            throw new ReglaDeNegocioDeclinadaException($"{Mensajes.TodasLasFechasConAusencia}: {detalle}");
        }

        var colaboradorDominio = MapearColaboradorProgramado(command.Colaborador);

        var enFirme = new List<DateOnly>();
        var enBorrador = new List<DateOnly>();
        foreach (var delMes in clasificacion.Libres.GroupBy(f => (f.Year, f.Month)).OrderBy(g => g.Key))
        {
            var fechasMes = delMes.Order().ToList();
            var streamId = BorradorMensual.ComputarStreamId(command.Colaborador.CodigoColaborador, fechasMes[0]);
            var borrador = await _eventStore.GetAggregateRootAsync<BorradorMensual>(streamId, ct);
            if (borrador is null)
            {
                var inicial = new ProgramacionTurnoAgregadaAlBorrador(
                    command.Id, colaboradorDominio, fechasMes, turnoProgramado, sedeSolicitada, jornadaProgramada);
                _eventStore.StartStream(BorradorMensual.Iniciar(inicial));
                enBorrador.AddRange(fechasMes);
                continue;
            }

            var firmes = borrador.Programar(
                command.Id, colaboradorDominio, fechasMes, turnoProgramado, sedeSolicitada, jornadaProgramada);
            enFirme.AddRange(firmes);
            enBorrador.AddRange(fechasMes.Except(firmes));
        }

        var evento = new ProgramacionTurnoSolicitada(
            command.Id, colaboradorDominio, enFirme, turnoProgramado, sedeSolicitada, jornadaProgramada);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        if (enFirme.Count > 0)
        {
            var colaborador = MapearResumenColaborador(command.Colaborador);
            // Los mapeos viven aqui: esta Function App es el unico proyecto que ve ambos ensamblados
            // (CA-ADR-0029 decision #5, payload por rol).
            var eventoPrivado = new ProgramacionTurnoEnFechasSolicitada(
                command.Id, colaborador, enFirme.AsReadOnly(), MapearTurno(turnoProgramado),
                DetalleJornadaMapeo.Mapear(jornadaProgramada), MapearSede(sedeSolicitada));

            await _privateEventSender.PublishAsync(new PublishOptions { GroupId = colaborador.CodigoColaborador }, eventoPrivado);
        }

        return new ResultadoSolicitudProgramacion(
            clasificacion.ConAusencia.Select(f => new FechaRespetada(f.Fecha, f.Motivo.ToString())).ToList(),
            enBorrador);
    }

    // La terna fluye tal cual del body: componer "{Tipo}-{Numero}" o concatenar nombres es del
    // cliente contra el maestro Colaboradores, no del servidor.
    private static ResumenColaborador MapearResumenColaborador(ColaboradorSolicitado colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    private static ColaboradorProgramado MapearColaboradorProgramado(ColaboradorSolicitado colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

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
