using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoEnFechasFunction;

public class SolicitarProgramacionTurnoEnFechasCommandHandlerTests
    : CommandHandlerAsyncTest<SolicitarProgramacionTurnoEnFechas, ResultadoSolicitudProgramacion>
{
    private static readonly PublishOptions OpcionesGrupoColaborador = new() { GroupId = "E001" };

    private static readonly Guid TurnoId = Guid.Parse("018e4c1a-4f2b-7000-8000-aabbccddeeff");
    private static readonly Guid TurnoIncompletoId = Guid.Parse("018e4c1a-4f2b-7000-8000-99aabbccddee");
    private static readonly Guid JornadaPorDefectoId = Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861a");

    private static readonly DateOnly Dia1 = new(2026, 10, 1);
    private static readonly DateOnly Dia2 = new(2026, 10, 2);
    private static readonly DateOnly Dia3 = new(2026, 10, 3);
    private static readonly DateOnly Dia4 = new(2026, 10, 4);
    private static readonly DateOnly Dia5 = new(2026, 10, 5);

    private const string StreamAusencias = "ac:E001";
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000001337");

    private static readonly ColaboradorSolicitado Colaborador = new("CC-12345678", "E001", "Juan Perez");
    private static readonly ResumenColaborador ColaboradorResumen = new("CC-12345678", "E001", "Juan Perez");
    private static readonly ColaboradorProgramado ColaboradorProgramadoEsperado = new("CC-12345678", "E001", "Juan Perez");

    private static readonly DetalleTurno DetalleEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly TurnoProgramado TurnoProgramadoEsperado = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly SedeProgramada SedePrincipal = new("SEDE-01", "Sede Principal");
    private static readonly DetalleSede SedePrincipalDetalle = new("SEDE-01", "Sede Principal");

    private static readonly TurnoProgramado TurnoConSedeAplicadaEsperado = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00", SedePrincipal)
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly DetalleTurno DetalleConSedeAplicadaEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00", SedePrincipalDetalle)
        }.AsReadOnly(),
        "06:00-14:00");

    private static LimitesJornada Limites42() => LimitesJornada.Crear(
        HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1);

    private static readonly JornadaProgramada JornadaEsperada = new(JornadaPorDefectoId, Limites42());
    private static readonly DetalleJornada DetalleJornadaEsperado = new(JornadaPorDefectoId, 2520, 510, 240, 1);

    private FakeAseguradorJornadaPredeterminada _asegurador = new(JornadaPorDefectoId);

    public SolicitarProgramacionTurnoEnFechasCommandHandlerTests()
    {
        Given(JornadaPorDefectoId.ToString(), JornadaCreada.Crear(JornadaPorDefectoId, Limites42()));
    }

    protected override ICommandHandlerAsync<SolicitarProgramacionTurnoEnFechas, ResultadoSolicitudProgramacion> Handler =>
        new SolicitarProgramacionTurnoEnFechasCommandHandler(EventStore, PrivateEventSender, _asegurador);

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(
            TurnoId,
            "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    private static AusenciaProgramada Ausencia(DateOnly inicio, DateOnly fin) =>
        new(AusenciaId, ColaboradorProgramadoEsperado, inicio, fin, MotivoAusencia.Vacaciones);

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_PersisteSoloLasLibresYPublicaUnEvento_CuandoUnaAusenciaCubreParteDeLasFechas()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given(StreamAusencias, Ausencia(Dia2, Dia3));

        var resultado = await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1, Dia2, Dia3, Dia4, Dia5]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1, Dia4, Dia5], TurnoProgramadoEsperado,
            jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1, Dia4, Dia5], DetalleEsperado, DetalleJornadaEsperado));
        PrivateEventSender.PublishedWithOptions.SelectMany(p => p.Events).Should().ContainSingle()
            .Which.Should().BeOfType<ProgramacionTurnoEnFechasSolicitada>();
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 3);
        resultado.FechasRespetadas.Should().BeEquivalentTo(new[]
        {
            new FechaRespetada(Dia2, "Vacaciones"),
            new FechaRespetada(Dia3, "Vacaciones"),
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_PublicaUnSoloEventoConTodasLasFechas_CuandoNoHayAusencias()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        var resultado = await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1, Dia2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1, Dia2], TurnoProgramadoEsperado,
            jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1, Dia2], DetalleEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 2);
        resultado.FechasRespetadas.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_AplicaLaCascadaDeSedeEnAmbosPayloads_CuandoLaSolicitudTraeSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1], TurnoConSedeAplicadaEsperado,
            SedePrincipal, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1], DetalleConSedeAplicadaEsperado,
                DetalleJornadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_AseguraLaJornadaPredeterminada_CuandoNoTraeJornadaId()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1], TurnoProgramadoEsperado,
            jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1], DetalleEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
        _asegurador.Invocaciones.Should().Be(1);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaRecursoYaExisteException_CuandoSolicitudYaExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1], TurnoProgramadoEsperado,
            jornada: JornadaEsperada));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.SolicitudYaExiste}*");
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaRecursoNoEncontradoException_CuandoTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(GuidAggregateId.ToString());
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEstaRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurno(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.TurnoRetirado}*");
        Then(GuidAggregateId.ToString());
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEstaIncompleto()
    {
        Given(TurnoIncompletoId.ToString(), TurnoCreado.Crear(TurnoIncompletoId, "Turno Incompleto", []));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoIncompletoId, Colaborador, [Dia1]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.TurnoIncompleto}*");
        Then(GuidAggregateId.ToString());
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], JornadaId: Guid.NewGuid()));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaReglaDeNegocioDeclinadaException_CuandoTodasLasFechasTienenAusencia()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given(StreamAusencias, Ausencia(Dia2, Dia3));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia2, Dia3]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.TodasLasFechasConAusencia}*")
            .WithMessage("*Vacaciones*");
        Then(GuidAggregateId.ToString());
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }
}
