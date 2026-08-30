// Al cancelar, el dia queda sin plan para efectos de calculo (DetalleTurno = null); la memoria del
// acto vive en el stream y en la solicitud de Programacion -- por eso el no-op de CA-3/CA-4 no deja
// evento de constancia.

using Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
// Esta isla declara su propio ResumenColaborador: el alias fija cual de los dos homonimos
// es el que trae el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
// Alias de tipo: DiaDepurado/MarcacionDelDia/HorasDiscriminadas existen homonimos en
// ControlHoras.DomainEvents (payload por rol, MEF-ADR-0039 decision #6); este handler republica al
// bus, asi que usa los del bus.
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada;

public class CancelacionTurnoDiarioSolicitadaEventHandlerTests
    : PrivateEventHandlerAsyncTest<CancelacionTurnoDiarioSolicitada>
{
    private static readonly Guid SolicitudCancelacionId =
        Guid.Parse("019600b0-0000-7000-8000-000000000010");

    private static readonly Guid SolicitudAsignacionId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    // ColaboradorProgramado (ControlHoras.DomainEvents) es el tipo que persiste TurnoDiarioAsignado
    // y TurnoDiarioCancelado.
    private static readonly ColaboradorProgramado Colaborador = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    // Mismo colaborador en la forma de bus (PrivateEvents.Colaboradores.ResumenColaborador). Los dos
    // literales se mantienen separados para que una permutacion de campos en el mapeo entre islas
    // se delate aqui, mismo criterio que ProgramacionTurnoDiarioSolicitadaEventHandlerTests.
    private static readonly ResumenColaborador ColaboradorResumen = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    // Stream ID determinista que el handler debe computar internamente a partir del evento.
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static readonly TurnoDiario TurnoDiarioTest = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "");

    protected override IPrivateEventHandlerAsync<CancelacionTurnoDiarioSolicitada> Handler =>
        new CancelacionTurnoDiarioSolicitadaEventHandler(EventStore, PrivateEventSender);

    private static CancelacionTurnoDiarioSolicitada CrearEvento() =>
        new(SolicitudCancelacionId, ColaboradorResumen, Fecha);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado() =>
        new(StreamId, Colaborador, Fecha, TurnoDiarioTest, SolicitudAsignacionId);

    private static TurnoDiarioCancelado CrearTurnoDiarioCancelado() =>
        TurnoDiarioCancelado.Crear(StreamId, Colaborador, Fecha, SolicitudCancelacionId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestampNormalizado) =>
        new(StreamId, Colaborador.CodigoColaborador, timestampNormalizado, "ENTRADA", "DEV-001");

    // CA-1: dia con turno asignado y con marcaciones -> se persiste TurnoDiarioCancelado,
    // DetalleTurno queda null y se publica DiaDepurado con las marcaciones crudas sin desglose.
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_CancelaElTurnoYRepublicaConMarcacionesCrudas_CuandoElDiaTieneTurnoYMarcaciones()
    {
        var timestampEntrada = new DateTime(2026, 3, 15, 8, 3, 0);
        Given(StreamId,
            CrearTurnoDiarioAsignado(),
            CrearMarcacionAdicionada(timestampEntrada));

        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioCancelado());
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 0);
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.Marcaciones.Count, 1);

        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            null,
            [],
            [new MarcacionDelDia(timestampEntrada, "ENTRADA")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
    }

    // CA-2: dia con turno asignado y SIN marcaciones -> cancelado efectivo, DiaDepurado sin franjas
    // ni horas (DesgloseHoras.Vacio: constante del VO, no derivada de Consolidar bajo prueba).
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_CancelaElTurno_CuandoElDiaTieneTurnoYNoTieneMarcaciones()
    {
        Given(StreamId, CrearTurnoDiarioAsignado());

        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioCancelado());
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        And<ControlDiarioAggregateRoot, DesgloseHoras>(StreamId, c => c.DesgloseHoras, DesgloseHoras.Vacio);

        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            null,
            [],
            [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
    }

    // CA-3: no-op silencioso -- el stream no existe (nunca hubo TurnoDiarioAsignado ni
    // MarcacionAdicionada para este colaborador+fecha).
    // Sin And<>(): un aggregate que nunca existio no se puede reconstruir desde el TestStore
    // (ArgumentNullException), asi que la asercion de estado se apoya en Then/ThenIsPublishedPrivately
    // vacios.
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElStreamNoExiste()
    {
        // Sin Given - el stream cd:EMP-001:20260315 no existe

        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
    }

    // Redelivery del mismo mensaje (Service Bus entrega at-least-once): el dia ya quedo sin plan
    // por una cancelacion previa, asi que la segunda pasada cae en el mismo no-op que CA-4 -- sin
    // segundo TurnoDiarioCancelado en el stream ni republicacion de DiaDepurado.
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElTurnoYaFueCancelado()
    {
        Given(StreamId,
            CrearTurnoDiarioAsignado(),
            CrearTurnoDiarioCancelado());

        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
    }

    // CA-4: no-op identico a CA-3, pero el stream SI existe (nacio solo por marcaciones, sin turno
    // asignado nunca). Las marcaciones existentes quedan intactas.
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElDiaExisteSinTurnoAsignado()
    {
        var timestampEntrada = new DateTime(2026, 3, 15, 8, 3, 0);
        Given(StreamId, CrearMarcacionAdicionada(timestampEntrada));

        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
    }
}
