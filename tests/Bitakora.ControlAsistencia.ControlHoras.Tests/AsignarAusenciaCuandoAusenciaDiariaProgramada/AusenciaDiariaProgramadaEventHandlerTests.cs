using Bitakora.ControlAsistencia.ControlHoras.AsignarAusenciaCuandoAusenciaDiariaProgramada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarAusenciaCuandoAusenciaDiariaProgramada;

public class AusenciaDiariaProgramadaEventHandlerTests
    : PrivateEventHandlerAsyncTest<AusenciaDiariaProgramada>
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

    protected override IPrivateEventHandlerAsync<AusenciaDiariaProgramada> Handler =>
        new AusenciaDiariaProgramadaEventHandler(EventStore, PrivateEventSender);

    private static AusenciaDiariaProgramada CrearEvento(Guid ausenciaId, string motivo) =>
        new(ausenciaId, ColaboradorResumen, Fecha, motivo);

    private static AusenciaDiariaAsignada CrearAusenciaAsignada(Guid ausenciaId, string motivo) =>
        AusenciaDiariaAsignada.Crear(StreamId, Colaborador, Fecha, ausenciaId, motivo);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado() =>
        new(StreamId, Colaborador, Fecha, TurnoDiarioTest, SolicitudAsignacionId);

    private static MarcacionAdicionada CrearMarcacion(DateTime timestamp, string tipo) =>
        new(StreamId, Colaborador.CodigoColaborador, timestamp, tipo, "DEV-001");

    private static HorasDiscriminadas SinHoras() => new(new Dictionary<string, decimal>(), []);

    private static DiaDepurado DiaConAusencia(string motivo, params MarcacionDelDia[] marcaciones) =>
        new(Colaborador.CodigoColaborador, Fecha, ColaboradorResumen, null, [], marcaciones, SinHoras(), motivo);

    // CA-1
    [Fact]
    public async Task AusenciaDiariaProgramada_AsignaLaAusenciaSinTocarElTurno_CuandoElDiaTieneTurnoProgramado()
    {
        Given(StreamId, CrearTurnoDiarioAsignado());

        await WhenAsync(CrearEvento(AusenciaId, Motivo));

        Then(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo));
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, "Turno Manana");
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 0);
        ThenIsPublishedPrivately(DiaConAusencia(Motivo));
    }

    // CA-2
    [Fact]
    public async Task AusenciaDiariaProgramada_IniciaElStream_CuandoElDiaNoTieneStream()
    {
        await WhenAsync(CrearEvento(AusenciaId, Motivo));

        Then(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo));
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        ThenIsPublishedPrivately(DiaConAusencia(Motivo));
    }

    // CA-3 (marcaciones previas a la ausencia, guardadas fuera de orden)
    [Fact]
    public async Task AusenciaDiariaProgramada_PublicaMarcacionesCrudasEnOrdenCronologico_CuandoElDiaYaTeniaMarcaciones()
    {
        var salida = new DateTime(2026, 3, 15, 16, 2, 0);
        var entrada = new DateTime(2026, 3, 15, 8, 3, 0);
        Given(StreamId, CrearTurnoDiarioAsignado(), CrearMarcacion(salida, "SALIDA"), CrearMarcacion(entrada, "ENTRADA"));

        await WhenAsync(CrearEvento(AusenciaId, Motivo));

        Then(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo));
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.Marcaciones.Count, 2);
        ThenIsPublishedPrivately(DiaConAusencia(Motivo,
            new MarcacionDelDia(entrada, "ENTRADA"),
            new MarcacionDelDia(salida, "SALIDA")));
    }

    // CA-3 (marcacion posterior a una ausencia ya asignada, sin franjas ni horas)
    [Fact]
    public async Task AusenciaDiariaProgramada_ConservaMarcacionesPosterioresALaAusenciaAnterior_CuandoLaReemplaza()
    {
        var entrada = new DateTime(2026, 3, 15, 8, 3, 0);
        Given(StreamId, CrearTurnoDiarioAsignado(), CrearAusenciaAsignada(AusenciaId, Motivo), CrearMarcacion(entrada, "ENTRADA"));

        await WhenAsync(CrearEvento(OtraAusenciaId, OtroMotivo));

        Then(StreamId, CrearAusenciaAsignada(OtraAusenciaId, OtroMotivo));
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 0);
        ThenIsPublishedPrivately(DiaConAusencia(OtroMotivo, new MarcacionDelDia(entrada, "ENTRADA")));
    }

    // CA-5 (reentrega)
    [Fact]
    public async Task AusenciaDiariaProgramada_NoEmiteNada_CuandoLlegaLaMismaAusenciaId()
    {
        Given(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo));

        await WhenAsync(CrearEvento(AusenciaId, Motivo));

        Then(StreamId);
        And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamId, c => c.DetalleTurno, null);
        ThenIsPublishedPrivately();
    }

    // CA-5 (reemplazo)
    [Fact]
    public async Task AusenciaDiariaProgramada_ReemplazaLaAusenciaAnterior_CuandoLlegaOtraAusenciaId()
    {
        Given(StreamId, CrearAusenciaAsignada(AusenciaId, Motivo));

        await WhenAsync(CrearEvento(OtraAusenciaId, OtroMotivo));

        Then(StreamId, CrearAusenciaAsignada(OtraAusenciaId, OtroMotivo));
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        ThenIsPublishedPrivately(DiaConAusencia(OtroMotivo));
    }
}
