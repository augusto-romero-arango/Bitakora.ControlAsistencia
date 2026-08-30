// Issue #499: Cancelar el turno diario del control diario al recibir la cancelacion de programacion.
// Lado ControlHoras de la cadena de "Cancelar Programacion" (#498): el dia queda sin plan para
// efectos de calculo (DetalleTurno = null); la memoria del acto vive en el stream y en la solicitud
// de Programacion -- por eso no hay evento de constancia en el no-op (CA-3/CA-4).

using Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaFunction.EventHandler;
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

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaFunction;

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

    // CA-7 (heredado de #322/#420): stream ID determinista que el handler debe computar internamente.
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
        new(StreamId, Colaborador, Fecha, SolicitudCancelacionId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestampNormalizado) =>
        new(StreamId, Colaborador.CodigoColaborador, timestampNormalizado, "ENTRADA", "DEV-001");

    // CA-1: dia con turno asignado y con marcaciones -> se persiste TurnoDiarioCancelado,
    // DetalleTurno queda null y se publica DiaDepurado con las marcaciones crudas sin desglose
    // (sin plan no hay depuracion -- reversion del extinto #422, ya resuelta por
    // Depurar/RecalcularDesgloseHoras via DetalleTurno null).
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

    // CA-3: no-op silencioso acordado con el experto -- el stream no existe (nunca hubo
    // TurnoDiarioAsignado ni MarcacionAdicionada para este colaborador+fecha). Sin evento de
    // constancia: la auditoria del acto quedo en Programacion. Precedente en este mismo proyecto
    // (SedeDeMarcacionResuelta_LanzaInvalidOperationException_CuandoElControlDiarioNoExiste): un
    // aggregate que nunca existio no se puede reconstruir con And<>() (ArgumentNullException,
    // TestStore.cs), asi que este test se apoya solo en Then/ThenIsPublishedPrivately vacios.
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElStreamNoExiste()
    {
        // Sin Given - el stream cd:EMP-001:20260315 no existe

        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
    }

    // CA-4: no-op identico a CA-3, pero el stream SI existe (nacio solo por marcaciones, sin turno
    // asignado nunca). El aggregate declina con resultado (Tell-don't-Ask, MEF-ADR-0012): el handler
    // no interroga DetalleTurno antes de decidir. Las marcaciones existentes quedan intactas.
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
