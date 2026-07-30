// HU-12 / issue #210: Asignar turno diario al control cuando llega ProgramacionTurnoDiarioSolicitada.
// ADR-0024 #8: el evento privado intra-BC se consume directo con IPrivateEventHandlerAsync, sin comando espejo.

using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class ProgramacionTurnoDiarioSolicitadaEventHandlerTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de EmpleadoId+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // CA-7: stream ID determinista que el handler debe computar internamente
    private static readonly string StreamId = $"{Empleado.EmpleadoId}:{Fecha:yyyy-MM-dd}";

    private static readonly DetalleTurno DetalleTurnoTest = new(
        "Turno Manana",
        [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [])]);

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PublicEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento() =>
        new(SolicitudId, Empleado, Fecha, DetalleTurnoTest);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado() =>
        new(StreamId, Empleado, Fecha, DetalleTurnoTest, SolicitudId);

    // CA-3: NO existe ControlDiario para EmpleadoId+Fecha - el handler inicia el stream
    // CA-5: el evento incluye InformacionEmpleado, Fecha, DetalleTurno y SolicitudId
    // CA-6: el aggregate actualiza InformacionEmpleado, Fecha y DetalleTurno
    // CA-7: el stream ID resultante es "{EmpleadoId}:{Fecha:yyyy-MM-dd}"
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_EmiteTurnoDiarioAsignado_CuandoNoExisteControlDiario()
    {
        // Sin Given - el stream no existe para este EmpleadoId+Fecha
        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioAsignado());
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        And<ControlDiarioAggregateRoot, InformacionEmpleado?>(StreamId, c => c.InformacionEmpleado, Empleado);
        And<ControlDiarioAggregateRoot, DateOnly>(StreamId, c => c.Fecha, Fecha);
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, DetalleTurnoTest.Nombre);
    }

    // CA-4: YA existe ControlDiario para EmpleadoId+Fecha - el handler agrega al stream existente
    // CA-5: el nuevo evento contiene todos los campos actualizados
    // CA-8: el segundo mensaje opera sobre el mismo stream (mismo EmpleadoId+Fecha = mismo StreamId)
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_EmiteTurnoDiarioAsignado_CuandoYaExisteControlDiario()
    {
        var solicitudAnteriorId = Guid.Parse("019600b0-0000-7000-8000-000000000002");
        var turnoAnterior = new TurnoDiarioAsignado(StreamId, Empleado, Fecha, DetalleTurnoTest, solicitudAnteriorId);

        // Pre-carga el stream con el mismo StreamId que usara el handler (CA-8)
        Given(StreamId, turnoAnterior);
        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioAsignado());
        And<ControlDiarioAggregateRoot, Guid>(StreamId, c => c.UltimaSolicitudId, SolicitudId);
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, DetalleTurnoTest.Nombre);
    }
}
