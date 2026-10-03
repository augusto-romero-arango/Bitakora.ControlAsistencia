using Bitakora.ControlAsistencia.ControlHoras.CancelarAusenciaCuandoAusenciaDiariaCancelada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using FranjaDepurada = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.FranjaDepurada;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.CancelarAusenciaCuandoAusenciaDiariaCancelada;

public class AusenciaDiariaCanceladaEventHandlerTests
    : PrivateEventHandlerAsyncTest<AusenciaDiariaCancelada>
{
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000020");
    private static readonly Guid OtraAusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000021");
    private static readonly Guid SolicitudAsignacionId = Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private const string Motivo = "Vacaciones";
    private const string OtroMotivo = "Incapacidad medica";

    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly ResumenColaborador ColaboradorResumen = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static readonly TurnoDiario TurnoDiarioTest = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "");

    protected override IPrivateEventHandlerAsync<AusenciaDiariaCancelada> Handler =>
        new AusenciaDiariaCanceladaEventHandler(EventStore, PrivateEventSender);

    private static AusenciaDiariaCancelada CrearEvento(Guid ausenciaId) =>
        new(ausenciaId, ColaboradorResumen, Fecha);

    private static CancelacionAusenciaDiariaRegistrada CrearCancelacion(Guid ausenciaId) =>
        CancelacionAusenciaDiariaRegistrada.Crear(StreamId, ausenciaId, Fecha);

    private static AusenciaDiariaAsignada CrearAusenciaAsignada(Guid ausenciaId, string motivo) =>
        AusenciaDiariaAsignada.Crear(StreamId, Colaborador, Fecha, ausenciaId, motivo);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado() =>
        new(StreamId, Colaborador, Fecha, TurnoDiarioTest, SolicitudAsignacionId);

    private static MarcacionAdicionada CrearMarcacion(DateTime timestamp) =>
        new(StreamId, Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static HorasDiscriminadas SinHoras() => new(new Dictionary<string, decimal>(), []);

    // CA-1
    [Fact]
    public async Task AusenciaDiariaCancelada_LiberaElDiaYPublicaElTurnoSinMotivo_CuandoLaAusenciaVigenteEsLaCancelada()
    {
        Given(StreamId, CrearTurnoDiarioAsignado(), CrearAusenciaAsignada(AusenciaId, Motivo));

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId, CrearCancelacion(AusenciaId));
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, "Turno Manana");
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 1);
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Manana",
            [new FranjaDepurada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, null, null, true)],
            [],
            SinHoras()));
    }

    // CA-2: esperado a mano; 2026-03-15 es domingo, 08:00-16:00 completo = 8h dominical diurna.
    [Fact]
    public async Task AusenciaDiariaCancelada_DepuraLasMarcacionesContraElTurno_CuandoElDiaTeniaMarcaciones()
    {
        var entrada = new DateTime(2026, 3, 15, 8, 0, 0);
        var salida = new DateTime(2026, 3, 15, 16, 0, 0);
        Given(StreamId,
            CrearTurnoDiarioAsignado(),
            CrearAusenciaAsignada(AusenciaId, Motivo),
            CrearMarcacion(entrada),
            CrearMarcacion(salida));

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId, CrearCancelacion(AusenciaId));
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 1);
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Manana",
            [new FranjaDepurada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, entrada, salida, false)],
            [new MarcacionDelDia(entrada, "ENTRADA"), new MarcacionDelDia(salida, "ENTRADA")],
            new HorasDiscriminadas(
                new Dictionary<string, decimal> { ["DominicalFestivaDiurna"] = 8.00m },
                [
                    $"{IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(8, 0)), new MomentoDelDia(new TimeOnly(16, 0)))}: " +
                    $"{IntervaloClasificado.Mensajes.Etiqueta(Concepto.DominicalFestivaDiurna)}"
                ])));
    }

    // CA-3
    [Fact]
    public async Task AusenciaDiariaCancelada_PublicaDiaSinProgramarSinMotivo_CuandoElDiaNoTeniaTurno()
    {
        Given(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo));

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId, CrearCancelacion(AusenciaId));
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador, Fecha, ColaboradorResumen, null, [], [], SinHoras()));
    }

    // CA-4 (parte 1)
    [Fact]
    public async Task AusenciaDiariaCancelada_IniciaElStreamYRecuerdaLaCancelacion_CuandoElStreamNoExiste()
    {
        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId, CrearCancelacion(AusenciaId));
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        ThenIsPublishedPrivately();
    }

    // CA-5
    [Fact]
    public async Task AusenciaDiariaCancelada_RecuerdaLaCancelacionSinLiberar_CuandoLaAusenciaVigenteEsOtra()
    {
        Given(StreamId, CrearAusenciaAsignada(OtraAusenciaId, OtroMotivo));

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId, CrearCancelacion(AusenciaId));
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 0);
        ThenIsPublishedPrivately();
    }

    [Fact]
    public async Task AusenciaDiariaCancelada_RecuerdaLaCancelacion_CuandoElDiaNoTieneAusenciaVigente()
    {
        Given(StreamId, CrearTurnoDiarioAsignado());

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId, CrearCancelacion(AusenciaId));
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, "Turno Manana");
        ThenIsPublishedPrivately();
    }

    // CA-6
    [Fact]
    public async Task AusenciaDiariaCancelada_NoEmiteNada_CuandoLaAusenciaYaFueCancelada()
    {
        Given(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo), CrearCancelacion(AusenciaId));

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        ThenIsPublishedPrivately();
    }

    [Fact]
    public async Task AusenciaDiariaCancelada_NoEmiteNada_CuandoLaCancelacionYaFueRecordadaSinAusencia()
    {
        Given(StreamId, CrearCancelacion(AusenciaId));

        await WhenAsync(CrearEvento(AusenciaId));

        Then(StreamId);
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        ThenIsPublishedPrivately();
    }
}
