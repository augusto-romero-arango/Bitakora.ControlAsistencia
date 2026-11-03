using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarProgramacionFunction;

public class CancelarProgramacionCommandHandlerTests
    : CommandHandlerAsyncTest<CancelarProgramacion>
{
    private static readonly PublishOptions OpcionesGrupoColaborador = new() { GroupId = "E001" };

    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    // Identificacion llega ya compuesta como "{Tipo}-{Numero}" y NombreCompleto ya concatenado:
    // los compone el cliente contra el maestro Colaboradores, nunca este servidor.
    private static readonly ColaboradorSolicitado Colaborador =
        new("CC-12345678", "E001", "Juan Perez");

    // Los mismos tres valores en los otros dos roles de payload -- bus (PrivateEvents) y evento
    // persistido (Programacion.DomainEvents): tres islas, un tipo por rol (MEF-ADR-0039 decision 2).
    private static readonly ResumenColaborador ColaboradorResumen =
        new("CC-12345678", "E001", "Juan Perez");

    private static readonly ColaboradorProgramado ColaboradorProgramadoEsperado =
        new("CC-12345678", "E001", "Juan Perez");

    protected override ICommandHandlerAsync<CancelarProgramacion> Handler =>
        new CancelarProgramacionCommandHandler(EventStore, PrivateEventSender);

    // CA-1
    [Fact]
    public async Task CancelarProgramacion_EmiteCancelacionSolicitadaYPublicaEvento_CuandoDatosValidos()
    {
        Given();
        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1]));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, new CancelacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorResumen, Fecha1));
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // CA-1
    [Fact]
    public async Task CancelarProgramacion_PublicaUnEventoPorCadaFecha_CuandoHayMultiplesFechas()
    {
        Given();
        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1, Fecha2]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1, Fecha2]));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Fecha1),
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Fecha2));
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 2);
    }

    // CA-1: delata que alguno de los dos mapeos componga o permute un campo de la terna.
    [Fact]
    public async Task CancelarProgramacion_PersisteLaTernaDeIdentidadDelColaborador_CuandoDatosValidos()
    {
        Given();
        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1]));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador, new CancelacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorResumen, Fecha1));
        And<SolicitudCancelacionAggregateRoot, ColaboradorProgramado?>(
            s => s.Colaborador, ColaboradorProgramadoEsperado);
    }

    [Fact]
    public async Task CancelarProgramacion_LanzaRecursoYaExisteException_CuandoSolicitudYaExiste()
    {
        Given(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1]));

        var act = async () => await WhenAsync(
            new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1]));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CancelarProgramacionCommandHandler.Mensajes.SolicitudYaExiste}*");
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    private const string StreamOctubre = "bm:E001:202610";
    private const string StreamNoviembre = "bm:E001:202611";

    private static readonly Guid SolicitudOrigen = Guid.Parse("019600a0-0000-7000-8000-000000001406");

    private static readonly TurnoProgramado TurnoManana = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly JornadaProgramada Jornada = new(
        Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861a"),
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1));

    private static DateOnly Oct(int dia) => new(2026, 10, dia);
    private static DateOnly Nov(int dia) => new(2026, 11, dia);

    private static ProgramacionTurnoAgregadaAlBorrador EnBorrador(params DateOnly[] fechas) =>
        new(SolicitudOrigen, ColaboradorProgramadoEsperado, fechas, TurnoManana, null, Jornada);

    private static BorradorDeProgramacionDescartado Descartado(params DateOnly[] fechas) =>
        new(ColaboradorProgramadoEsperado, fechas);

    private static int FechasRestantes(BorradorMensual b) => b.FechasEnBorrador.Count;

    // CA-1
    [Fact]
    public async Task CancelarProgramacion_EmiteBorradorDescartadoConLasFechasCanceladas_CuandoEstanEnElBorrador()
    {
        Given(StreamOctubre, EnBorrador(Oct(5), Oct(6), Oct(7)));

        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Oct(5), Oct(6)]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Oct(5), Oct(6)]));
        Then(StreamOctubre, Descartado(Oct(5), Oct(6)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(5)),
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(6)));
        And<BorradorMensual, int>(StreamOctubre, FechasRestantes, 1);
    }

    // CA-2
    [Fact]
    public async Task CancelarProgramacion_EnviaMensajeDeLasFechasDelBorrador_CuandoSeCancelan()
    {
        Given(StreamOctubre, EnBorrador(Oct(5)));

        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Oct(5), Oct(8)]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Oct(5), Oct(8)]));
        Then(StreamOctubre, Descartado(Oct(5)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(5)),
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(8)));
        And<BorradorMensual, int>(StreamOctubre, FechasRestantes, 0);
    }

    // CA-3
    [Fact]
    public async Task CancelarProgramacion_DescartaEnCadaBorradorMensual_CuandoLasFechasCruzanMesYSonSueltas()
    {
        Given(StreamOctubre, EnBorrador(Oct(30), Oct(31)));
        Given(StreamNoviembre, EnBorrador(Nov(2), Nov(3), Nov(4), Nov(5)));

        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Oct(30), Nov(2), Nov(5)]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Oct(30), Nov(2), Nov(5)]));
        Then(StreamOctubre, Descartado(Oct(30)));
        Then(StreamNoviembre, Descartado(Nov(2), Nov(5)));
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(30)),
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Nov(2)),
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Nov(5)));
        And<BorradorMensual, int>(StreamOctubre, FechasRestantes, 1);
        And<BorradorMensual, int>(StreamNoviembre, FechasRestantes, 2);
    }

    // CA-4
    [Fact]
    public async Task CancelarProgramacion_NoEmiteEventoDeBorrador_CuandoElBorradorNoTieneNingunaFecha()
    {
        Given(StreamOctubre, EnBorrador(Oct(20)));

        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Oct(5)]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Oct(5)]));
        Then(StreamOctubre);
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(5)));
        And<BorradorMensual, int>(StreamOctubre, FechasRestantes, 1);
    }

    // CA-4: sin borrador en el mes no se crea ninguno; el unico stream tocado es el de la solicitud.
    [Fact]
    public async Task CancelarProgramacion_NoCreaBorrador_CuandoElMesNoTieneBorrador()
    {
        Given();

        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Oct(5)]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Oct(5)]));
        Then(StreamOctubre);
        ThenIsPublishedPrivately(OpcionesGrupoColaborador,
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Oct(5)));
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // CA-4: el 409 por Id duplicado no toca el borrador.
    [Fact]
    public async Task CancelarProgramacion_NoDescartaNada_CuandoSolicitudYaExiste()
    {
        Given(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Oct(5)]));
        Given(StreamOctubre, EnBorrador(Oct(5)));

        var act = async () => await WhenAsync(
            new CancelarProgramacion(GuidAggregateId, Colaborador, [Oct(5)]));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CancelarProgramacionCommandHandler.Mensajes.SolicitudYaExiste}*");
        Then(StreamOctubre);
        ThenIsPublishedPrivately();
        And<BorradorMensual, int>(StreamOctubre, FechasRestantes, 1);
    }
}
