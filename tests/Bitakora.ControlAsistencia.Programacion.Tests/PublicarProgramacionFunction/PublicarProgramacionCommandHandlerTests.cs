using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction.CommandHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.PublicarProgramacionFunction;

public class PublicarProgramacionCommandHandlerTests : CommandHandlerAsyncTest<PublicarProgramacion>
{
    private static readonly PublishOptions OpcionesGrupoColaborador = new() { GroupId = "E001" };

    private const string Codigo = "E001";
    private const string StreamOctubre = "bm:E001:202610";
    private const string StreamNoviembre = "bm:E001:202611";
    private const string StreamAusencias = "ac:E001";

    private static readonly Guid Solicitud1 = Guid.Parse("019600a0-0000-7000-8000-000000001405");
    private static readonly Guid Solicitud2 = Guid.Parse("019600a0-0000-7000-8000-000000001406");
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000001407");
    private static readonly Guid JornadaId = Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861a");

    private static readonly ColaboradorProgramado Colaborador = new("CC-12345678", Codigo, "Juan Perez");
    private static readonly ResumenColaborador Resumen = new("CC-12345678", Codigo, "Juan Perez");

    private static readonly TurnoProgramado TurnoManana = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly DetalleTurno DetalleManana = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly TurnoProgramado TurnoTarde = new(
        "Turno Tarde",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], "14:00-22:00")
        }.AsReadOnly(),
        "14:00-22:00");

    private static readonly DetalleTurno DetalleTarde = new(
        "Turno Tarde",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(14, 0), new TimeOnly(22, 0), 0, [], [], "14:00-22:00")
        }.AsReadOnly(),
        "14:00-22:00");

    private static readonly SedeProgramada Sede = new("SEDE-01", "Sede Principal");
    private static readonly DetalleSede SedeDetalle = new("SEDE-01", "Sede Principal");

    private static readonly JornadaProgramada Jornada = new(
        JornadaId,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1));

    private static readonly DetalleJornada DetalleJornadaEsperado = new(JornadaId, 2520, 510, 240, 1);

    protected override ICommandHandlerAsync<PublicarProgramacion> Handler =>
        new PublicarProgramacionCommandHandler(EventStore, PrivateEventSender);

    private static DateOnly Oct(int dia) => new(2026, 10, dia);
    private static DateOnly Nov(int dia) => new(2026, 11, dia);

    private static PublicarProgramacion Comando(DateOnly desde, DateOnly hasta) => new(Codigo, desde, hasta);

    private static ProgramacionTurnoAgregadaAlBorrador Agregada(
        Guid solicitud, TurnoProgramado turno, SedeProgramada? sede, params DateOnly[] fechas) =>
        new(solicitud, Colaborador, fechas, turno, sede, Jornada);

    private static ProgramacionPublicada Publicada(params DateOnly[] fechas) => new(Colaborador, fechas);

    private static BorradorDeProgramacionDescartado Descartado(params DateOnly[] fechas) => new(Colaborador, fechas);

    private static ProgramacionTurnoEnFechasSolicitada Lote(
        Guid solicitud, DetalleTurno turno, DetalleSede? sede, params DateOnly[] fechas) =>
        new(solicitud, Resumen, fechas, turno, DetalleJornadaEsperado, sede);

    private static int EnBorrador(BorradorMensual b) => b.FechasEnBorrador.Count;

    [Fact]
    public async Task PublicarProgramacion_EmiteProgramacionPublicadaYEnviaElLote_CuandoElRangoCubreElBorrador()
    {
        Given(StreamOctubre, Agregada(Solicitud1, TurnoManana, Sede, Oct(5), Oct(6), Oct(7)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(5), Oct(6), Oct(7)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            Lote(Solicitud1, DetalleManana, SedeDetalle, Oct(5), Oct(6), Oct(7)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_EnviaUnMensajePorSolicitudDeOrigen_CuandoLaSegundaSobrescribioUnaFecha()
    {
        Given(StreamOctubre,
            Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6), Oct(7)),
            Agregada(Solicitud2, TurnoTarde, null, Oct(6)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(5), Oct(6), Oct(7)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            Lote(Solicitud1, DetalleManana, null, Oct(5), Oct(7)),
            Lote(Solicitud2, DetalleTarde, null, Oct(6)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_PublicaSoloLasFechasDelRango_CuandoElBorradorTieneFechasFuera()
    {
        Given(StreamOctubre, Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6), Oct(7), Oct(8)));

        await WhenAsync(Comando(Oct(6), Oct(7)));

        Then(StreamOctubre, Publicada(Oct(6), Oct(7)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, Lote(Solicitud1, DetalleManana, null, Oct(6), Oct(7)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 2);
    }

    [Fact]
    public async Task PublicarProgramacion_PublicaLasQueQuedaban_CuandoUnaPublicacionPosteriorCubreSuRango()
    {
        Given(StreamOctubre,
            Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6), Oct(7), Oct(8)),
            Publicada(Oct(6), Oct(7)));

        await WhenAsync(Comando(Oct(5), Oct(8)));

        Then(StreamOctubre, Publicada(Oct(5), Oct(8)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, Lote(Solicitud1, DetalleManana, null, Oct(5), Oct(8)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_EmiteUnaPublicacionPorMesYUnSoloMensaje_CuandoElRangoCruzaMes()
    {
        Given(StreamOctubre, Agregada(Solicitud1, TurnoManana, null, Oct(28), Oct(29), Oct(30), Oct(31)));
        Given(StreamNoviembre, Agregada(Solicitud1, TurnoManana, null, Nov(1), Nov(2), Nov(3)));

        await WhenAsync(Comando(Oct(28), Nov(3)));

        Then(StreamOctubre, Publicada(Oct(28), Oct(29), Oct(30), Oct(31)));
        Then(StreamNoviembre, Publicada(Nov(1), Nov(2), Nov(3)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            Lote(Solicitud1, DetalleManana, null, Oct(28), Oct(29), Oct(30), Oct(31), Nov(1), Nov(2), Nov(3)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
        And<BorradorMensual, int>(StreamNoviembre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_NoPublicaNiEnviaLaFechaConAusencia_CuandoLaAusenciaEstaVigente()
    {
        Given(StreamOctubre, Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6), Oct(7)));
        Given(StreamAusencias,
            new AusenciaProgramada(AusenciaId, Colaborador, Oct(6), Oct(6), MotivoAusencia.Vacaciones));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(5), Oct(7)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, Lote(Solicitud1, DetalleManana, null, Oct(5), Oct(7)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 1);
    }

    [Fact]
    public async Task PublicarProgramacion_PublicaLaFechaQueQuedoEnBorrador_CuandoLaAusenciaSeCancelo()
    {
        Given(StreamOctubre,
            Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6), Oct(7)),
            Publicada(Oct(5), Oct(7)));
        Given(StreamAusencias,
            new AusenciaProgramada(AusenciaId, Colaborador, Oct(6), Oct(6), MotivoAusencia.Vacaciones),
            new AusenciaCancelada(AusenciaId, Colaborador, [Oct(6)]));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(6)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, Lote(Solicitud1, DetalleManana, null, Oct(6)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_NoEmiteNiEnvia_CuandoNingunMesDelRangoTieneBorrador()
    {
        Given(StreamAusencias,
            new AusenciaProgramada(AusenciaId, Colaborador, Oct(6), Oct(6), MotivoAusencia.Vacaciones));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then();
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, int>(StreamAusencias, a => a.Ausencias.Count, 1);
    }

    [Fact]
    public async Task PublicarProgramacion_NoTocaElBorradorDeOtroMes_CuandoElRangoNoLoCubre()
    {
        Given(StreamNoviembre, Agregada(Solicitud1, TurnoManana, null, Nov(1), Nov(2), Nov(3)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamNoviembre);
        ThenIsPublishedPrivately();
        And<BorradorMensual, int>(StreamNoviembre, EnBorrador, 3);
    }

    [Fact]
    public async Task PublicarProgramacion_NoEmiteNiEnvia_CuandoElBorradorNoTieneFechasEnElRango()
    {
        Given(StreamOctubre, Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6)));

        await WhenAsync(Comando(Oct(10), Oct(20)));

        Then(StreamOctubre);
        ThenIsPublishedPrivately();
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 2);
    }

    [Fact]
    public async Task PublicarProgramacion_NoEmiteNiEnvia_CuandoTodoElRangoEstaCubiertoPorAusencias()
    {
        Given(StreamOctubre, Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6)));
        Given(StreamAusencias,
            new AusenciaProgramada(AusenciaId, Colaborador, Oct(5), Oct(6), MotivoAusencia.IncapacidadMedica));

        await WhenAsync(Comando(Oct(5), Oct(6)));

        Then(StreamOctubre);
        ThenIsPublishedPrivately();
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 2);
    }

    [Fact]
    public async Task PublicarProgramacion_EnviaDescansosYExtrasDeLaFranja_CuandoElTurnoLosTiene()
    {
        var turnoConSubFranjas = new TurnoProgramado(
            "Turno Mixto",
            new List<FranjaProgramada>
            {
                new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                    new List<SubFranjaProgramada> { new(new TimeOnly(10, 0), new TimeOnly(10, 30), 0, 0, "Desayuno") }.AsReadOnly(),
                    new List<SubFranjaProgramada> { new(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, "Extra") }.AsReadOnly(),
                    "06:00-14:00")
            }.AsReadOnly(),
            "06:00-14:00");
        var detalleEsperado = new DetalleTurno(
            "Turno Mixto",
            new List<DetalleFranjaOrdinaria>
            {
                new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                    new List<DetalleSubFranja> { new(new TimeOnly(10, 0), new TimeOnly(10, 30), 0, 0, "Desayuno") }.AsReadOnly(),
                    new List<DetalleSubFranja> { new(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, "Extra") }.AsReadOnly(),
                    "06:00-14:00")
            }.AsReadOnly(),
            "06:00-14:00");
        Given(StreamOctubre, Agregada(Solicitud1, turnoConSubFranjas, null, Oct(5)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(5)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, Lote(Solicitud1, detalleEsperado, null, Oct(5)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_NoEmiteNiEnvia_CuandoSeRepublicaLoYaPublicado()
    {
        Given(StreamOctubre,
            Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6)),
            Publicada(Oct(5), Oct(6)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre);
        ThenIsPublishedPrivately();
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_NoEmiteNiEnvia_CuandoElRangoYaSeDescarto()
    {
        Given(StreamOctubre,
            Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6)),
            Descartado(Oct(5), Oct(6)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre);
        ThenIsPublishedPrivately();
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_PublicaSoloLasQueNoSeDescartaron_CuandoUnDescartePrevioQuitoParteDelBorrador()
    {
        Given(StreamOctubre,
            Agregada(Solicitud1, TurnoManana, null, Oct(5), Oct(6), Oct(7), Oct(8)),
            Descartado(Oct(6), Oct(7)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(5), Oct(8)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, Lote(Solicitud1, DetalleManana, null, Oct(5), Oct(8)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task PublicarProgramacion_EnviaLaEstampaDelBorradorSinReestamparla_CuandoLaJornadaTeniaModalidad()
    {
        var estampada = new JornadaProgramada(
            JornadaId, Jornada.Limites, ModalidadJornada.Flexible, true, new FichaDeLey(540, 240, 2520));
        Given(StreamOctubre,
            new ProgramacionTurnoAgregadaAlBorrador(Solicitud1, Colaborador, [Oct(5)], TurnoManana, null, estampada));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Publicada(Oct(5)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new ProgramacionTurnoEnFechasSolicitada(
                Solicitud1, Resumen, [Oct(5)], DetalleManana,
                new DetalleJornada(JornadaId, 2520, 510, 240, 1, "Flexible", true, 540, 240, 2520)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }
}
