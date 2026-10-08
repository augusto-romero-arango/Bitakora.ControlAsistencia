using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

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

        // Guarda transaccional contra el aggregate ya cargado (Tell-don't-Ask, MEF-ADR-0012): un
        // turno solo es asignable a una nueva solicitud si esta activo y completo (CA-ADR-0033).
        switch (catalogo.EvaluarAsignabilidad())
        {
            case ResultadoAsignabilidadTurno.Retirado:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoRetirado);
            case ResultadoAsignabilidadTurno.Incompleto:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoIncompleto);
        }

        // Unico punto de normalizacion de la sede entrante: aguas abajo (cascada, evento
        // persistido, evento de bus) solo se transporta el valor ya normalizado.
        var sedeSolicitada = command.Sede?.ConCentroDeCostosNormalizado();

        // La cascada de sede se aplica ANTES de construir cualquier evento: ambos payloads
        // (persistido y de bus) derivan del mismo turno ya resuelto, un solo punto de resolucion.
        // Sin ramificar por si la solicitud trae sede -- con command.Sede null la cascada es
        // identidad (Tell-don't-Ask, MEF-ADR-0012).
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

        var fechas = clasificacion.Libres;

        var colaboradorDominio = MapearColaboradorProgramado(command.Colaborador);
        var evento = new ProgramacionTurnoSolicitada(
            command.Id, colaboradorDominio, fechas, turnoProgramado, sedeSolicitada, jornadaProgramada);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        // El comando HTTP trae sus propios DTOs y el evento privado lleva los tipos de
        // PrivateEvents: los mapeos viven aqui porque esta Function App es el unico proyecto que ve
        // ambos ensamblados (CA-ADR-0029 decision #5, payload por rol).
        var colaborador = MapearResumenColaborador(command.Colaborador);
        var detalleTurno = MapearTurno(turnoProgramado);
        var sede = MapearSede(sedeSolicitada);
        var detalleJornada = MapearJornada(jornadaProgramada);
        var eventosPrivados = fechas
            .Select(fecha => new ProgramacionTurnoDiarioSolicitada(
                command.Id, colaborador, fecha, detalleTurno, sede, detalleJornada))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
        return new ResultadoSolicitudProgramacion(
            clasificacion.ConAusencia.Select(f => new FechaRespetada(f.Fecha, f.Motivo.ToString())).ToList());
    }

    // Issue #436 (fase B): la terna llega ya resuelta desde el body y fluye TAL CUAL a los dos
    // payloads -- aqui solo cambia el tipo, uno por isla. Murio la composicion transitoria que
    // armaba "{Tipo}-{Numero}" y concatenaba los nombres: eso lo hace el cliente contra el maestro
    // Colaboradores (#330), no el servidor.
    private static ResumenColaborador MapearResumenColaborador(ColaboradorSolicitado colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    private static ColaboradorProgramado MapearColaboradorProgramado(ColaboradorSolicitado colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    // Unico punto de traduccion desde TurnoProgramado (dominio) hacia el payload de bus, incluidas
    // las listas anidadas de franjas y sub-franjas.
    private static DetalleTurno MapearTurno(TurnoProgramado turno) =>
        new(turno.Nombre,
            turno.FranjasOrdinarias.Select(MapearFranja).ToList().AsReadOnly(),
            turno.Descripcion);

    private static DetalleFranjaOrdinaria MapearFranja(FranjaProgramada franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Extras.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Descripcion,
            MapearSede(franja.Sede));

    private static DetalleSubFranja MapearSubFranja(SubFranjaProgramada subFranja) =>
        new(subFranja.HoraInicio, subFranja.HoraFin, subFranja.DiaOffsetInicio,
            subFranja.DiaOffsetFin, subFranja.Descripcion);

    // Unico punto de mapeo SedeProgramada -> DetalleSede. Opcional: null se conserva.
    private static DetalleSede? MapearSede(SedeProgramada? sede) =>
        sede is null ? null : new DetalleSede(sede.Id, sede.Nombre, sede.CentroDeCostos);

    private static DetalleJornada MapearJornada(JornadaProgramada jornada) =>
        new(jornada.JornadaId,
            jornada.Limites.HorasSemanales.TotalMinutos(),
            jornada.Limites.TopeDiario.TotalMinutos(),
            jornada.Limites.MinimoDiario.TotalMinutos(),
            jornada.Limites.DiasDescansoPorSemana);
}
