using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
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

        // La cascada de sede se aplica ANTES de construir cualquier evento: ambos payloads
        // (persistido y de bus) derivan del mismo turno ya resuelto, un solo punto de resolucion.
        // Sin ramificar por si la solicitud trae sede -- con command.Sede null la cascada es
        // identidad (Tell-don't-Ask, MEF-ADR-0012).
        var turnoProgramado = catalogo.ObtenerDetalle().ConSedePorDefecto(command.Sede);
        var fechas = command.Fechas.AsReadOnly();

        var colaboradorDominio = MapearColaboradorProgramado(command.Colaborador);
        var evento = new ProgramacionTurnoSolicitada(
            command.Id, colaboradorDominio, fechas, turnoProgramado, command.Sede);
        var solicitud = SolicitudProgramacionAggregateRoot.Iniciar(evento);

        _eventStore.StartStream(solicitud);

        // El comando HTTP trae los tipos de PublicEvents y el evento privado lleva los de
        // PrivateEvents: los mapeos viven aqui porque esta Function App es el unico proyecto que ve
        // ambos ensamblados (CA-ADR-0029 decision #5, payload por rol).
        var colaborador = MapearResumenColaborador(command.Colaborador);
        var detalleTurno = MapearTurno(turnoProgramado);
        var sede = MapearSede(command.Sede);
        var eventosPrivados = command.Fechas
            .Select(fecha => new ProgramacionTurnoDiarioSolicitada(
                command.Id, colaborador, fecha, detalleTurno, sede))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
    }

    // Unico punto donde el quinteto del body HTTP se comprime a la terna que cruza el bus, con el
    // contrato del maestro Colaboradores ("{Tipo}-{Numero}" y el nombre ya concatenado). Cualquier
    // otro consumidor de la terna debe pedirla aqui, no recomponerla. TRANSITORIO: muere cuando la
    // fase A del corte (issue #433) tenga su fase B y el body HTTP tambien lleve la terna.
    private static ResumenColaborador MapearResumenColaborador(InformacionColaborador colaborador) =>
        new($"{colaborador.TipoIdentificacion}-{colaborador.NumeroIdentificacion}",
            colaborador.CodigoColaborador,
            $"{colaborador.Nombres} {colaborador.Apellidos}");

    private static ColaboradorProgramado MapearColaboradorProgramado(InformacionColaborador colaborador) =>
        new(colaborador.CodigoColaborador, colaborador.TipoIdentificacion, colaborador.NumeroIdentificacion,
            colaborador.Nombres, colaborador.Apellidos);

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
        sede is null ? null : new DetalleSede(sede.Id, sede.Nombre);
}
