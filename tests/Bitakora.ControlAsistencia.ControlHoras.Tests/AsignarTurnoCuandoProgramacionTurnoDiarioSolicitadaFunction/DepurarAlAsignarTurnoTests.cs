// HU-123: Integrar depurador al ControlDiario de forma reactiva
// Familia 2: verifica que Apply(TurnoDiarioAsignado) dispara el recalculo de ControlesDeFranja

using AwesomeAssertions;
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

    protected override ICommandHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler(EventStore);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
        new(SolicitudId, Empleado, Fecha, detalleTurno);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    // CA-4: con 2 MarcacionAdicionada previas (07:00, 15:00) sin turno,
    //        al procesar ProgramacionTurnoDiarioSolicitada con franja 06:00-14:00,
    //        el Apply(TurnoDiarioAsignado) dispara Depurar() y ControlesDeFranja
    //        queda con ControlFranja(Franja06_14, Entrada=07:00, Salida=15:00).
    //        Verifica el caso "marcaciones llegaron antes que el turno".
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
    }
}
