using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
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
        var turnoProgramado = catalogo.ObtenerDetalle();
        var fechas = command.Fechas.AsReadOnly();

        // Issue #319 CA-2/CA-5: el comando HTTP conserva InformacionEmpleado (PublicEvents, fuera
        // de alcance); el evento persistido tipa con Empleado (dominio) -- el mapeo vive aqui.
        var empleadoDominio = MapearEmpleadoDominio(command.Empleado);
        var evento = new ProgramacionTurnoSolicitada(command.Id, empleadoDominio, fechas, turnoProgramado);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        // ADR-0024 decision #2/#3: ProgramacionTurnoDiarioSolicitada es intra-BC (lo consume
        // ControlHoras, mismo BC) -> IPrivateEvent publicado al ASB interno via IPrivateEventSender.
        // CA-ADR-0029 decision #5 (payload por rol): el comando HTTP trae InformacionEmpleado
        // (PublicEvents) y el evento privado lleva DetalleEmpleado, asi que el mapeo vive aqui --
        // la Function App es el unico proyecto que ve ambos ensamblados.
        var empleado = MapearEmpleado(command.Empleado);
        // Issue #319 CA-5: DetalleTurno (PrivateEvents) se deriva de TurnoProgramado (dominio),
        // no directamente del catalogo -- unico punto de mapeo hacia el payload de bus.
        var detalleTurno = MapearTurno(turnoProgramado);
        var eventosPrivados = command.Fechas
            .Select(fecha => new ProgramacionTurnoDiarioSolicitada(
                command.Id, empleado, fecha, detalleTurno))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
    }

    private static DetalleEmpleado MapearEmpleado(InformacionEmpleado empleado) =>
        new(empleado.EmpleadoId, empleado.TipoIdentificacion, empleado.NumeroIdentificacion,
            empleado.Nombres, empleado.Apellidos);

    // Stub de fase roja (issue #319 CA-5) -- el implementer completa el mapeo campo a campo hacia
    // el record propio del dominio (Programacion.DomainEvents.Empleado).
    private static Empleado MapearEmpleadoDominio(InformacionEmpleado empleado) =>
        throw new NotImplementedException();

    // Stub de fase roja (issue #319 CA-5) -- el implementer completa el mapeo (incluyendo las
    // listas anidadas de franjas y sub-franjas) desde TurnoProgramado (dominio) hacia DetalleTurno
    // (PrivateEvents), el unico punto de traduccion hacia el payload que cruza el bus interno.
    private static DetalleTurno MapearTurno(TurnoProgramado turno) =>
        throw new NotImplementedException();
}
