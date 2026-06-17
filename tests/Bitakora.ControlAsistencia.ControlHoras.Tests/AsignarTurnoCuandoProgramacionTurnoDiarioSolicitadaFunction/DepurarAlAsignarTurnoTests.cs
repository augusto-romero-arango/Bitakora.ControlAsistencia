// HU-123: Integrar depurador al ControlDiario de forma reactiva
// HU-131: Emitir DiaCalculado tras asignar turno (CA-1, CA-2)
// Familia 2: verifica que Apply(TurnoDiarioAsignado) dispara el recalculo de ControlesDeFranja
// y que el handler publica DiaCalculado via IPublicEventSender tras el recalculo.

using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class DepurarAlAsignarTurnoTests
    : CommandHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de EmpleadoId+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600c0-0000-7000-8000-000000000002");

    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{Empleado.EmpleadoId}:{Fecha:yyyy-MM-dd}";

    // CA-4: franja unica 06:00-14:00 para el turno asignado
    private static readonly DetalleFranjaOrdinaria Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], []);

    // Timestamps de las marcaciones que llegan antes que el turno
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    // HU-131: handler ahora requiere IPublicEventSender para publicar DiaCalculado
    protected override ICommandHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler(EventStore, PublicEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
        new(SolicitudId, Empleado, Fecha, detalleTurno);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    // CA-1 (HU-123): con 2 MarcacionAdicionada previas (07:00, 15:00) sin turno,
    //        al procesar ProgramacionTurnoDiarioSolicitada con franja 06:00-14:00,
    //        el Apply(TurnoDiarioAsignado) dispara Depurar() y ControlesDeFranja
    //        queda con ControlFranja(Franja06_14, Entrada=07:00, Salida=15:00).
    //        Verifica el caso "marcaciones llegaron antes que el turno".
    // CA-1 (HU-131): el handler publica DiaCalculado con InformacionEmpleado, Fecha
    //        y ControlesDeFranja actualizados (Entrada y Salida presentes, EsAnomala=false).
    [Fact]
    public async Task DebeCalcularControlFranja_CuandoMarcacionesLlegaronAntesQueTurno()
    {
        var turnoConFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);
        Given(StreamId,
            CrearMarcacionAdicionada(Timestamp07_00),
            CrearMarcacionAdicionada(Timestamp15_00));

        await WhenAsync(CrearEvento(turnoConFranjaUnica));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoConFranjaUnica));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, Timestamp15_00) });

        // HU-131 CA-1: publica DiaCalculado con los ControlesDeFranja tras la depuracion.
        // EsAnomala=false porque Entrada=07:00 y Salida=15:00 estan presentes.
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            new[] { new DetalleControlFranja(Franja06_14, Timestamp07_00, Timestamp15_00, false) },
            DesgloseHoras.Vacio));
    }

    // CA-2 (HU-131): sin marcaciones previas, el Apply(TurnoDiarioAsignado) dispara Depurar()
    //        pero sin marcaciones el depurador retorna una franja con Entrada=null, Salida=null.
    //        DiaCalculado se emite aunque todos los ControlesDeFranja sean anomalos.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PublicaDiaCalculado_CuandoNoHayMarcacionesPrevias()
    {
        // Sin Given - stream nuevo, sin marcaciones previas
        var turnoConFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);

        await WhenAsync(CrearEvento(turnoConFranjaUnica));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoConFranjaUnica));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, null, null) }); // EsAnomala=true

        // HU-131 CA-2: DiaCalculado se publica aunque todos los controles sean anomalos.
        // EsAnomala=true porque Entrada y Salida son null.
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            new[] { new DetalleControlFranja(Franja06_14, null, null, true) },
            DesgloseHoras.Vacio));
    }

    // CA-2 (HU-131): turno sin franjas ordinarias genera ControlesDeFranja vacio.
    //        DiaCalculado se emite igual con lista vacia de controles.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PublicaDiaCalculado_CuandoTurnoNoTieneFranjas()
    {
        // Sin Given - stream nuevo, turno sin franjas ordinarias
        var turnoSinFranjas = new DetalleTurno("Turno Sin Franjas", []);

        await WhenAsync(CrearEvento(turnoSinFranjas));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoSinFranjas));
        And<ControlDiarioAggregateRoot, int>(
            StreamId,
            c => c.ControlesDeFranja.Count, 0);

        // HU-131 CA-2: DiaCalculado se publica aunque ControlesDeFranja este vacio.
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            [],
            DesgloseHoras.Vacio));
    }
}
