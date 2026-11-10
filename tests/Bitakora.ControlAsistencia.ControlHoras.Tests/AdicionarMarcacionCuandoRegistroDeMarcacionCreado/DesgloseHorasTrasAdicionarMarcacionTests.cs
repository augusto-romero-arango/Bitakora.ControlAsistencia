// Verifica la integracion: Apply(MarcacionAdicionada) recalcula DesgloseHoras despues de Depurar. La
// matematica de compensacion entre franjas la cubren los tests de ConsolidadorDesgloseHoras.

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

public class DesgloseHorasTrasAdicionarMarcacionTests : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static readonly ColaboradorProgramado Colaborador = new(
        "CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000003");

    // Descripcion (dato derivado) no interviene en estos tests: placeholder "".
    private static readonly FranjaProgramada Franja08_12 =
        new(new TimeOnly(8, 0), new TimeOnly(12, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");

    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp08_00 = new(2026, 3, 15, 8, 0, 0);
    private static readonly DateTime Timestamp12_05 = new(2026, 3, 15, 12, 5, 0);
    private static readonly DateTime Timestamp14_10 = new(2026, 3, 15, 14, 10, 0);
    private static readonly DateTime Timestamp18_30 = new(2026, 3, 15, 18, 30, 0);

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PrivateEventSender);

    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(DateTime timestamp) =>
        new(CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario detalleTurno) =>
        new(StreamId, Colaborador, Fecha, detalleTurno, SolicitudId);

    [Fact]
    public async Task RegistroDeMarcacionCreado_RecalculaDesgloseHoras_CuandoMarcacionCompletaUltimaFranja()
    {
        var turnoPartido = new TurnoDiario("Turno Partido", [Franja08_12, Franja14_18], "");
        Given(StreamId,
            CrearTurnoDiarioAsignado(turnoPartido),
            CrearMarcacionAdicionada(Timestamp08_00),
            CrearMarcacionAdicionada(Timestamp12_05),
            CrearMarcacionAdicionada(Timestamp14_10));

        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp18_30));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp18_30));

        var controlFranja1 = new ControlFranja(Franja08_12, Timestamp08_00, Timestamp12_05);
        var controlFranja2 = new ControlFranja(Franja14_18, Timestamp14_10, Timestamp18_30);
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { controlFranja1, controlFranja2 });

        // Esperado a mano, sin ejecutar Consolidar ni CalcularDesglose (domingo 2026-03-15):
        //   F1 trabajada 08:00-12:05: ordinaria 08:00-12:00 + excedente 12:00-12:05, sin retardo.
        //   F2 trabajada 14:10-18:30: el retardo 14:00-14:10 se compensa con los ultimos 10 min del
        //   excedente; ordinaria 14:10-18:00 + excedente visible 18:00-18:20.
        var f1Ordinaria = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(8, 0)),
                new MomentoDelDia(new TimeOnly(12, 0))),
            Concepto.DominicalFestivaDiurna);
        var f1Excedente = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(12, 0)),
                new MomentoDelDia(new TimeOnly(12, 5))),
            Concepto.ExtraDiurnaDominicalFestiva);
        var franja1 = new DesgloseFranja(Franja08_12, [f1Ordinaria, f1Excedente], Retardo.Vacio);

        var f2Ordinaria = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(14, 10)),
                new MomentoDelDia(new TimeOnly(18, 0))),
            Concepto.DominicalFestivaDiurna);
        var f2Excedente = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(18, 0)),
                new MomentoDelDia(new TimeOnly(18, 20))),
            Concepto.ExtraDiurnaDominicalFestiva);
        var retardoF2 = Retardo.Crear(
            [IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(14, 0)), new MomentoDelDia(new TimeOnly(14, 10)))],
            [IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(18, 20)), new MomentoDelDia(new TimeOnly(18, 30)))]);
        var franja2 = new DesgloseFranja(Franja14_18, [f2Ordinaria, f2Excedente], retardoF2);

        var esperado = new DesgloseHoras([franja1, franja2], retardoF2, FranjasAnomalas: 0);

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            esperado);
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_DejaDesgloseHorasVacio_CuandoNoHayTurnoPrevio()
    {
        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            DesgloseHoras.Vacio);
    }
}
