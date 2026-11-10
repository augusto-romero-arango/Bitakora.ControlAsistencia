using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

public class RegistroDeMarcacionCreadoEventHandlerTests
    : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    private const string CodigoColaborador = "EMP-001";

    private static readonly DateTime TimestampDiurno = new DateTime(2026, 3, 15, 8, 15, 0);

    private static readonly DateTime TimestampDeMadrugada = new DateTime(2026, 3, 15, 2, 30, 0);

    private static readonly string StreamIdDia15 = $"cd:{CodigoColaborador}:20260315";
    private static readonly string StreamIdDia14 = $"cd:{CodigoColaborador}:20260314";

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PrivateEventSender);

    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(
        DateTime timestampNormalizado,
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(CodigoColaborador, timestampNormalizado, tipoMarcacion, dispositivoId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(
        string streamId,
        DateTime timestampNormalizado,
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(streamId, CodigoColaborador, timestampNormalizado, tipoMarcacion, dispositivoId);

    [Fact]
    public async Task RegistroDeMarcacionCreado_CreaControlDiarioConMarcacion_CuandoNoExisteYHoraFueraDeVentanaNocturna()
    {
        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampDiurno));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampDiurno));
        And<ControlDiarioAggregateRoot, ColaboradorProgramado?>(
            StreamIdDia15, c => c.InformacionColaborador, null);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(
            StreamIdDia15, c => c.DetalleTurno, null);
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, DateOnly>(
            StreamIdDia15, c => c.Fecha, new DateOnly(2026, 3, 15));
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_CreaSoloElControlDiarioDelDiaCalendario_CuandoHoraEsDeMadrugada()
    {
        // La marcacion de madrugada se guarda solo en su dia; el reparto la lleva a la vispera.
        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampDeMadrugada));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampDeMadrugada));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, DateOnly>(
            StreamIdDia15, c => c.Fecha, new DateOnly(2026, 3, 15));

        Then(StreamIdDia14);
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_AgregaMarcacionALaLista_CuandoControlDiarioYaExisteConMarcacionPrevia()
    {
        var marcacionPrevia = CrearMarcacionAdicionada(
            StreamIdDia15, new DateTime(2026, 3, 15, 7, 0, 0));

        Given(StreamIdDia15, marcacionPrevia);

        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampDiurno));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampDiurno));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 2);
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_CreaUnSoloControlDiario_CuandoHoraEsIgualALaHoraDeCorte()
    {
        var timestampEnCorte = new DateTime(2026, 3, 15, 4, 0, 0);

        await WhenAsync(CrearRegistroDeMarcacionCreado(timestampEnCorte));

        Then(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, timestampEnCorte));
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);

        Then(StreamIdDia14);
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_IgnoraMarcacion_CuandoExisteDuplicadoPorMinutoNormalizado()
    {
        var marcacionExistente = CrearMarcacionAdicionada(StreamIdDia15, TimestampDiurno);
        Given(StreamIdDia15, marcacionExistente);

        await WhenAsync(CrearRegistroDeMarcacionCreado(TimestampDiurno, "SALIDA", "DEV-002"));

        Then(StreamIdDia15);
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDia15, c => c.Marcaciones.Count, 1);
    }
}
