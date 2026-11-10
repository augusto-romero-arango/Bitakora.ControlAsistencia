using AwesomeAssertions;
// Al cancelar, el dia queda sin plan (DetalleTurno = null); la memoria del acto vive en el stream y en la
// solicitud de Programacion, por eso el no-op no deja evento de constancia.

using Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
// El alias fija cual de los dos ResumenColaborador homonimos trae el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
// Homonimos en ControlHoras.DomainEvents; el handler republica al bus, asi que usa los del bus.
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

    private static readonly ColaboradorProgramado Colaborador = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    // Literal separado de Colaborador a proposito: una permutacion de campos en el mapeo entre islas
    // se delata aqui.
    private static readonly ResumenColaborador ColaboradorResumen = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

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

        ThenIsPublishedPrivately(new PublishOptions { GroupId = Colaborador.CodigoColaborador }, new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            null,
            [],
            [new MarcacionDelDia(timestampEntrada, "ENTRADA")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), VersionControlDiario: 3));
    }

    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_CancelaElTurno_CuandoElDiaTieneTurnoYNoTieneMarcaciones()
    {
        Given(StreamId, CrearTurnoDiarioAsignado());

        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioCancelado());
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        And<ControlDiarioAggregateRoot, DesgloseHoras>(StreamId, c => c.DesgloseHoras, DesgloseHoras.Vacio);

        ThenIsPublishedPrivately(new PublishOptions { GroupId = Colaborador.CodigoColaborador }, new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            null,
            [],
            [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), VersionControlDiario: 2));
    }

    // Sin And<>(): un aggregate que nunca existio no se reconstruye desde el TestStore
    // (ArgumentNullException).
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElStreamNoExiste()
    {
        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }

    // Redelivery (Service Bus es at-least-once): el dia ya quedo sin plan, asi que cae en el mismo no-op.
    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElTurnoYaFueCancelado()
    {
        Given(StreamId,
            CrearTurnoDiarioAsignado(),
            CrearTurnoDiarioCancelado());

        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
    }

    [Fact]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElDiaExisteSinTurnoAsignado()
    {
        var timestampEntrada = new DateTime(2026, 3, 15, 8, 3, 0);
        Given(StreamId, CrearMarcacionAdicionada(timestampEntrada));

        await WhenAsync(CrearEvento());

        Then(StreamId);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.Marcaciones.Count, 1);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
    }
}
