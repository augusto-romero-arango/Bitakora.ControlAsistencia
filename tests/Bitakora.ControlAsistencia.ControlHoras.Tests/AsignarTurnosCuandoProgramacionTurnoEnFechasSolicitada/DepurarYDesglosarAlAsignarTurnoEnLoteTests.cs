using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using DiasDepurados = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiasDepurados;
using FranjaDepurada = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.FranjaDepurada;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada;

public class DepurarYDesglosarAlAsignarTurnoEnLoteTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoEnFechasSolicitada>
{
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000002");

    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly ResumenColaborador ColaboradorResumen = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static readonly FranjaProgramada Franja06_14 = new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");
    private static readonly DetalleFranjaOrdinaria Franja06_14Detalle = new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja06_12 = new(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], [], "");
    private static readonly DetalleFranjaOrdinaria Franja06_12Detalle = new(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja14_18 = new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");
    private static readonly DetalleFranjaOrdinaria Franja14_18Detalle = new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");

    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoEnFechasSolicitada> Handler =>
        new ProgramacionTurnoEnFechasSolicitadaEventHandler(EventStore, PrivateEventSender);

    private static ProgramacionTurnoEnFechasSolicitada CrearEvento(DetalleTurno detalleTurno) =>
        new(SolicitudId, ColaboradorResumen, [Fecha], detalleTurno, null!);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario turnoDiario) =>
        new(StreamId, Colaborador, Fecha, turnoDiario, SolicitudId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static string LineaConcepto(TimeOnly inicio, TimeOnly fin, Concepto concepto) =>
        $"{IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin))}: " +
        $"{IntervaloClasificado.Mensajes.Etiqueta(concepto)}";

    private DiaDepurado FotoUnica() =>
        PrivateEventSender.PublishedWithOptions.SelectMany(p => p.Events).OfType<DiasDepurados>()
            .Should().ContainSingle().Subject.Dias.Should().ContainSingle().Subject;

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_CalculaControlFranja_CuandoMarcacionesLlegaronAntesQueTurno()
    {
        Given(StreamId,
            CrearMarcacionAdicionada(Timestamp07_00),
            CrearMarcacionAdicionada(Timestamp15_00));

        await WhenAsync(CrearEvento(new DetalleTurno("Turno Manana", [Franja06_14Detalle], "")));

        Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14], "")));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, Timestamp15_00) });

        FotoUnica().Should().Be(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Manana",
            [new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Timestamp07_00, Timestamp15_00, false)],
            [new MarcacionDelDia(Timestamp07_00, "ENTRADA", Pertenencia: "DelTurno"), new MarcacionDelDia(Timestamp15_00, "ENTRADA", Pertenencia: "DelTurno")],
            new HorasDiscriminadas(
                new Dictionary<string, decimal> { ["DominicalFestivaDiurna"] = 7.00m },
                [LineaConcepto(new TimeOnly(7, 0), new TimeOnly(14, 0), Concepto.DominicalFestivaDiurna)]),
            VersionControlDiario: 3));
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_PublicaLaFoto_CuandoTurnoNoTieneFranjas()
    {
        await WhenAsync(CrearEvento(new DetalleTurno("Turno Sin Franjas", [], "")));

        Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Sin Franjas", [], "")));
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 0);

        FotoUnica().Should().Be(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Sin Franjas",
            [],
            [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []),
            VersionControlDiario: 1));
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_RecalculaDesgloseHoras_CuandoTurnoLlegaTrasMarcaciones()
    {
        Given(StreamId,
            CrearMarcacionAdicionada(Timestamp07_00),
            CrearMarcacionAdicionada(Timestamp15_00));

        await WhenAsync(CrearEvento(new DetalleTurno("Turno Manana", [Franja06_14Detalle], "")));

        Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14], "")));

        var ordinaria = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(7, 0)),
                new MomentoDelDia(new TimeOnly(14, 0))),
            Concepto.DominicalFestivaDiurna);

        var retardo = Retardo.Crear(
            [IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(6, 0)), new MomentoDelDia(new TimeOnly(7, 0)))],
            [IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(14, 0)), new MomentoDelDia(new TimeOnly(15, 0)))]);

        var esperado = new DesgloseHoras(
            [new DesgloseFranja(Franja06_14, [ordinaria], retardo)],
            retardo,
            FranjasAnomalas: 0);

        And<ControlDiarioAggregateRoot, DesgloseHoras>(StreamId, c => c.DesgloseHoras, esperado);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_DejaDesgloseHorasConFranjasAnomalas_CuandoTurnoSinMarcaciones()
    {
        await WhenAsync(CrearEvento(
            new DetalleTurno("Turno Partido", [Franja06_12Detalle, Franja14_18Detalle], "")));

        Then(StreamId, CrearTurnoDiarioAsignado(
            new TurnoDiario("Turno Partido", [Franja06_12, Franja14_18], "")));
        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId, c => c.DesgloseHoras, new DesgloseHoras([], Retardo.Vacio, 2));
    }
}
