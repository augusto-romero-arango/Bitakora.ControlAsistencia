using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;
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

        // Issue #319 (tres islas, MEF-ADR-0039 decision 2): ObtenerDetalle() ya no produce el DTO
        // de bus (DetalleTurno, PrivateEvents) -- produce el tipo propio del dominio
        // (TurnoProgramado, Programacion.DomainEvents). El FA mapea a DetalleTurno solo para los
        // eventos que cruzan el bus (CA-5).
        // Issue #341: la cascada de sede se aplica ANTES de construir cualquier evento -- ambos
        // payloads (persistido y bus) derivan del mismo turno ya resuelto (un solo punto de
        // resolucion). Sin ramificar por si la solicitud trae sede: con command.Sede null la
        // cascada es identidad (Tell-don't-Ask, MEF-ADR-0012).
        var turnoProgramado = catalogo.ObtenerDetalle().ConSedePorDefecto(command.Sede);
        var fechas = command.Fechas.AsReadOnly();

        // Issue #319 CA-2/CA-5: el comando HTTP conserva el tipo de PublicEvents (fuera de alcance);
        // el evento persistido tipa con ColaboradorProgramado (dominio) -- el mapeo vive aqui.
        var colaboradorDominio = MapearColaboradorProgramado(command.Empleado);
        var evento = new ProgramacionTurnoSolicitada(
            command.Id, colaboradorDominio, fechas, turnoProgramado, command.Sede);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        // ADR-0024 decision #2/#3: ProgramacionTurnoDiarioSolicitada es intra-BC (lo consume
        // ControlHoras, mismo BC) -> IPrivateEvent publicado al ASB interno via IPrivateEventSender.
        // CA-ADR-0029 decision #5 (payload por rol): el comando HTTP trae InformacionColaborador
        // (PublicEvents) y el evento privado lleva DetalleColaborador, asi que el mapeo vive aqui --
        // la Function App es el unico proyecto que ve ambos ensamblados.
        var colaborador = MapearDetalleColaborador(command.Empleado);
        // Issue #319 CA-5: DetalleTurno (PrivateEvents) se deriva de TurnoProgramado (dominio),
        // no directamente del catalogo -- unico punto de mapeo hacia el payload de bus.
        var detalleTurno = MapearTurno(turnoProgramado);
        // Issue #331: la sede es un gemelo deliberado (CA-ADR-0029 decision #5) -- mismo mapeo
        // campo a campo que ColaboradorProgramado/DetalleTurno hacia su forma de bus.
        var sede = MapearSede(command.Sede);
        var eventosPrivados = command.Fechas
            .Select(fecha => new ProgramacionTurnoDiarioSolicitada(
                command.Id, colaborador, fecha, detalleTurno, sede))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
    }

    private static DetalleColaborador MapearDetalleColaborador(InformacionColaborador colaborador) =>
        new(colaborador.EmpleadoId, colaborador.TipoIdentificacion, colaborador.NumeroIdentificacion,
            colaborador.Nombres, colaborador.Apellidos);

    // Issue #319 CA-5: mapeo campo a campo hacia el record propio del dominio
    // (Programacion.DomainEvents.ColaboradorProgramado), tipo del evento persistido
    // ProgramacionTurnoSolicitada.
    private static ColaboradorProgramado MapearColaboradorProgramado(InformacionColaborador colaborador) =>
        new(colaborador.EmpleadoId, colaborador.TipoIdentificacion, colaborador.NumeroIdentificacion,
            colaborador.Nombres, colaborador.Apellidos);

    // Issue #319 CA-5: unico punto de traduccion desde TurnoProgramado (dominio) hacia el payload
    // que cruza el bus interno (DetalleTurno, PrivateEvents), incluidas las listas anidadas de
    // franjas y sub-franjas.
    private static DetalleTurno MapearTurno(TurnoProgramado turno) =>
        new(turno.Nombre,
            turno.FranjasOrdinarias.Select(MapearFranja).ToList().AsReadOnly(),
            turno.Descripcion);

    // Issue #341: la sede EFECTIVA de la franja (ya resuelta por la cascada, ConSedePorDefecto)
    // se propaga al payload de bus con el mismo mapeo campo a campo que usa la sede de la
    // solicitud (MapearSede) -- gemelo deliberado, no una traduccion nueva.
    private static DetalleFranjaOrdinaria MapearFranja(FranjaProgramada franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Extras.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Descripcion,
            MapearSede(franja.Sede));

    private static DetalleSubFranja MapearSubFranja(SubFranjaProgramada subFranja) =>
        new(subFranja.HoraInicio, subFranja.HoraFin, subFranja.DiaOffsetInicio,
            subFranja.DiaOffsetFin, subFranja.Descripcion);

    // Issue #331: unico punto de mapeo desde SedeProgramada (dominio) hacia el payload que cruza
    // el bus interno (DetalleSede, PrivateEvents). Opcional: null se conserva.
    private static DetalleSede? MapearSede(SedeProgramada? sede) =>
        sede is null ? null : new DetalleSede(sede.Id, sede.Nombre);
}
