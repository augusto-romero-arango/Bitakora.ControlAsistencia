using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction.CommandHandler;
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

    private static readonly Guid TurnoDescansoId = Guid.Parse("018e4c1a-4f2b-7000-8000-aabbcc001122");
    private static readonly Guid TurnoRetiradoSinFranjasId = Guid.Parse("018e4c1a-4f2b-7000-8000-aabbcc003344");
    private static readonly Guid TurnoConSedePrearmadaId = Guid.Parse("018e4c1a-4f2b-7000-8000-778899aabbcc");
    private static readonly Guid TurnoConFranjasMixtasId = Guid.Parse("018e4c1a-4f2b-7000-8000-1a2b3c4d5e6f");

    private static readonly SedeProgramada SedeSuba = new("SEDE-SUBA", "Suba");
    private static readonly DetalleSede SedeSubaDetalle = new("SEDE-SUBA", "Suba");
    private static readonly SedeProgramada SedeConCentroDeCostos = new("SEDE-01", "Sede Principal", "CC-100");
    private static readonly DetalleSede SedeConCentroDeCostosDetalle = new("SEDE-01", "Sede Principal", "CC-100");
    private static readonly SedeProgramada SedeConCentroDeCostosEnBlanco = new("SEDE-01", "Sede Principal", "   ");

    private static TurnoCreado CrearEventoTurnoDeHorarioAbierto() =>
        TurnoCreado.CrearDeHorarioAbierto(
            TurnoId, "Turno Abierto", [new DatosFranja(new TimeOnly(4, 0), new TimeOnly(10, 0), [], [])]);

    private static readonly TurnoProgramado TurnoAbiertoProgramadoEsperado = new(
        "Turno Abierto",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(4, 0), new TimeOnly(10, 0), 0, [], [], "04:00-10:00")
        }.AsReadOnly(),
        "04:00-10:00",
        EsHorarioAbierto: true);

    private static readonly DetalleTurno DetalleAbiertoEsperado = new(
        "Turno Abierto",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(4, 0), new TimeOnly(10, 0), 0, [], [], "04:00-10:00")
        }.AsReadOnly(),
        "04:00-10:00",
        EsHorarioAbierto: true);

    private static VentanaDeEntradaModificada VentanaDeTresHoras() =>
        VentanaDeEntradaModificada.Crear(TurnoId, HorasYMinutos.Crear(3, 0));

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
    public async Task SolicitarProgramacionTurnoEnFechas_MapeaDescansosYExtrasEnAmbosPayloads_CuandoElTurnoTieneHijas()
    {
        Given(TurnoId.ToString(), TurnoCreado.Crear(
            TurnoId,
            "Turno Partido",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                [(new TimeOnly(13, 0), new TimeOnly(14, 0))])]));

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        const string descripcion = "06:00-14:00, descanso 10:00-10:15, extra 13:00-14:00";
        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            new TurnoProgramado(
                "Turno Partido",
                new List<FranjaProgramada>
                {
                    new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                        [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "10:00-10:15")],
                        [new SubFranjaProgramada(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, "13:00-14:00")],
                        descripcion)
                }.AsReadOnly(),
                descripcion),
            jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                new DetalleTurno(
                    "Turno Partido",
                    new List<DetalleFranjaOrdinaria>
                    {
                        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                            [new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "10:00-10:15")],
                            [new DetalleSubFranja(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, "13:00-14:00")],
                            descripcion)
                    }.AsReadOnly(),
                    descripcion),
                DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
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

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_CopiaLaAmplitudDelCatalogoEnAmbosPayloads_CuandoElTurnoTieneVentanaDeTresHoras()
    {
        Given(TurnoId.ToString(), CrearEventoTurno(), VentanaDeTresHoras());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1, Dia2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1, Dia2],
            TurnoProgramadoEsperado with { AmplitudVentanaEntradaEnMinutos = 180 }, jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1, Dia2],
                DetalleEsperado with { AmplitudVentanaEntradaEnMinutos = 180 }, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.DetalleTurno!.AmplitudVentanaEntradaEnMinutos, 180);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_DejaAmplitudEnCero_CuandoElTurnoEsFijo()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1], TurnoProgramadoEsperado, jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1], DetalleEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.DetalleTurno!.AmplitudVentanaEntradaEnMinutos, 0);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_ConservaLaAmplitud_CuandoLaSolicitudTraeSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurno(), VentanaDeTresHoras());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            TurnoConSedeAplicadaEsperado with { AmplitudVentanaEntradaEnMinutos = 180 },
            SedePrincipal, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                DetalleConSedeAplicadaEsperado with { AmplitudVentanaEntradaEnMinutos = 180 },
                DetalleJornadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.DetalleTurno!.AmplitudVentanaEntradaEnMinutos, 180);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_CopiaLaMarcaDeHorarioAbiertoEnAmbosPayloads_CuandoElTurnoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeHorarioAbierto());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1, Dia2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1, Dia2],
            TurnoAbiertoProgramadoEsperado, jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1, Dia2], DetalleAbiertoEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, bool>(s => s.DetalleTurno!.EsHorarioAbierto, true);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_DejaLaMarcaEnFalse_CuandoElTurnoNoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1], TurnoProgramadoEsperado, jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1], DetalleEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, bool>(s => s.DetalleTurno!.EsHorarioAbierto, false);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_ConservaLaMarcaDeHorarioAbierto_CuandoLaSolicitudTraeSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeHorarioAbierto());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            TurnoAbiertoProgramadoEsperado with
            {
                FranjasOrdinarias = new List<FranjaProgramada>
                {
                    new(new TimeOnly(4, 0), new TimeOnly(10, 0), 0, [], [], "04:00-10:00", SedePrincipal)
                }.AsReadOnly()
            },
            SedePrincipal, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                DetalleAbiertoEsperado with
                {
                    FranjasOrdinarias = new List<DetalleFranjaOrdinaria>
                    {
                        new(new TimeOnly(4, 0), new TimeOnly(10, 0), 0, [], [], "04:00-10:00", SedePrincipalDetalle)
                    }.AsReadOnly()
                },
                DetalleJornadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, bool>(s => s.DetalleTurno!.EsHorarioAbierto, true);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_ConservaLaSedeDeLaFranjaYAplicaLaSedePorDefecto_CuandoElTurnoTraeFranjasMixtas()
    {
        Given(TurnoConFranjasMixtasId.ToString(), TurnoCreado.Crear(
            TurnoConFranjasMixtasId,
            "Turno Mixto",
            [
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(10, 0), [], [], SedeSuba),
                new DatosFranja(new TimeOnly(14, 0), new TimeOnly(18, 0), [], [])
            ]));

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoConFranjasMixtasId, Colaborador, [Dia1], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            new TurnoProgramado(
                "Turno Mixto",
                new List<FranjaProgramada>
                {
                    new(new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], "06:00-10:00", SedeSuba),
                    new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "14:00-18:00", SedePrincipal)
                }.AsReadOnly(),
                "06:00-10:00, 14:00-18:00"),
            SedePrincipal, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                new DetalleTurno(
                    "Turno Mixto",
                    new List<DetalleFranjaOrdinaria>
                    {
                        new(new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], "06:00-10:00", SedeSubaDetalle),
                        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "14:00-18:00", SedePrincipalDetalle)
                    }.AsReadOnly(),
                    "06:00-10:00, 14:00-18:00"),
                DetalleJornadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede, SedeSuba);
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[1].Sede, SedePrincipal);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_DejaSedeEnNull_CuandoLaSolicitudNoIncluyeSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1], TurnoProgramadoEsperado,
            sede: null, jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1], DetalleEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(s => s.Sede, null);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_PersisteLaSedePrearmadaDelCatalogo_CuandoElTurnoLaTraePorFranja()
    {
        Given(TurnoConSedePrearmadaId.ToString(), TurnoCreado.Crear(
            TurnoConSedePrearmadaId,
            "Turno Con Sede",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], SedeSuba)]));

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoConSedePrearmadaId, Colaborador, [Dia1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            new TurnoProgramado(
                "Turno Con Sede",
                new List<FranjaProgramada>
                {
                    new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00", SedeSuba)
                }.AsReadOnly(),
                "06:00-14:00"),
            sede: null, jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                new DetalleTurno(
                    "Turno Con Sede",
                    new List<DetalleFranjaOrdinaria>
                    {
                        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00", SedeSubaDetalle)
                    }.AsReadOnly(),
                    "06:00-14:00"),
                DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede, SedeSuba);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_PersisteElCentroDeCostosDeLaSede_CuandoLaSedeLoTrae()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], SedeConCentroDeCostos));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            new TurnoProgramado(
                "Turno Manana",
                new List<FranjaProgramada>
                {
                    new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00", SedeConCentroDeCostos)
                }.AsReadOnly(),
                "06:00-14:00"),
            SedeConCentroDeCostos, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                new DetalleTurno(
                    "Turno Manana",
                    new List<DetalleFranjaOrdinaria>
                    {
                        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00", SedeConCentroDeCostosDetalle)
                    }.AsReadOnly(),
                    "06:00-14:00"),
                DetalleJornadaEsperado, SedeConCentroDeCostosDetalle));
        And<SolicitudProgramacionAggregateRoot, string?>(s => s.Sede!.CentroDeCostos, "CC-100");
        And<SolicitudProgramacionAggregateRoot, string?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede!.CentroDeCostos, "CC-100");
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_DejaCentroDeCostosEnNull_CuandoLaSedeNoLoTrae()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            TurnoConSedeAplicadaEsperado, SedePrincipal, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                DetalleConSedeAplicadaEsperado, DetalleJornadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, string?>(s => s.Sede!.CentroDeCostos, null);
        And<SolicitudProgramacionAggregateRoot, string?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede!.CentroDeCostos, null);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_NormalizaCentroDeCostosEnBlancoANull_CuandoLaSedeLoTraeVacio()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1], SedeConCentroDeCostosEnBlanco));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            TurnoConSedeAplicadaEsperado, SedePrincipal, JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                DetalleConSedeAplicadaEsperado, DetalleJornadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, string?>(s => s.Sede!.CentroDeCostos, null);
        And<SolicitudProgramacionAggregateRoot, string?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede!.CentroDeCostos, null);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_EmiteProgramacionSolicitada_CuandoElTurnoDelCatalogoEsDescanso()
    {
        Given(TurnoDescansoId.ToString(), TurnoCreado.CrearDescanso(TurnoDescansoId, "Descanso Compensatorio"));

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoDescansoId, Colaborador, [Dia1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1],
            new TurnoProgramado("Descanso Compensatorio", new List<FranjaProgramada>().AsReadOnly(), "(descanso)"),
            jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1],
                new DetalleTurno("Descanso Compensatorio", new List<DetalleFranjaOrdinaria>().AsReadOnly(), "(descanso)"),
                DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_LanzaReglaDeNegocioDeclinadaExceptionConMensajeDeRetirado_CuandoElTurnoRetiradoAdemasEstaIncompleto()
    {
        Given(TurnoRetiradoSinFranjasId.ToString(),
            TurnoCreado.Crear(TurnoRetiradoSinFranjasId, "Turno Retirado Vacio", []),
            TurnoRetirado.Crear(TurnoRetiradoSinFranjasId));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoRetiradoSinFranjasId, Colaborador, [Dia1]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{SolicitarProgramacionTurnoEnFechasCommandHandler.Mensajes.TurnoRetirado}*");
        Then(GuidAggregateId.ToString());
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_NoAfectaLasFechas_CuandoLaAusenciaEsDeOtroColaborador()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given("ac:E999", new AusenciaProgramada(
            AusenciaId, new ColaboradorProgramado("CC-999", "E999", "Otra Persona"),
            Dia1, Dia2, MotivoAusencia.Vacaciones));

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
    public async Task SolicitarProgramacionTurnoEnFechas_ProgramaTodasLasFechasYRespondeSinRespetadas_CuandoElColaboradorNoTieneAusencias()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        var resultado = await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1, Dia2, Dia3]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1, Dia2, Dia3], TurnoProgramadoEsperado,
            jornada: JornadaEsperada));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1, Dia2, Dia3], DetalleEsperado, DetalleJornadaEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 3);
        resultado.FechasRespetadas.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoEnFechas_EstampaLaJornadaExplicitaYNoConsultaAlAsegurador_CuandoTraeJornadaId()
    {
        var jid = Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861b");
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given(jid.ToString(), JornadaCreada.Crear(jid, Limites42()));

        await WhenAsync(new SolicitarProgramacionTurnoEnFechas(
            GuidAggregateId, TurnoId, Colaborador, [Dia1, Dia2], JornadaId: jid));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Dia1, Dia2], TurnoProgramadoEsperado,
            jornada: new JornadaProgramada(jid, Limites42())));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                GuidAggregateId, ColaboradorResumen, [Dia1, Dia2], DetalleEsperado,
                new DetalleJornada(jid, 2520, 510, 240, 1)));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 2);
        _asegurador.Invocaciones.Should().Be(0);
    }
}
