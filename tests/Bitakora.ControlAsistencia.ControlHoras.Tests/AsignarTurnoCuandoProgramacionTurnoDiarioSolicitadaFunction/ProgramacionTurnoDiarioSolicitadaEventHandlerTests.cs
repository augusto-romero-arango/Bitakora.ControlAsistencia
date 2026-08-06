// HU-12 / issue #210: Asignar turno diario al control cuando llega ProgramacionTurnoDiarioSolicitada.
// ADR-0024 #8: el evento privado intra-BC se consume directo con IPrivateEventHandlerAsync, sin comando espejo.

using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class ProgramacionTurnoDiarioSolicitadaEventHandlerTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de EmpleadoId+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    // Issue #322: Empleado (ControlHoras.DomainEvents) -- el tipo que persiste TurnoDiarioAsignado.
    private static readonly Empleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    // Mismo empleado, en la forma con que llega dentro del evento privado; el handler lo mapea
    // a Empleado para TurnoDiarioAsignado (CA-ADR-0029 decision #5).
    private static readonly DetalleEmpleado EmpleadoDetalle = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // CA-7: stream ID determinista que el handler debe computar internamente
    private static readonly string StreamId = $"{Empleado.EmpleadoId}:{Fecha:yyyy-MM-dd}";

    // El evento privado sigue trayendo DetalleTurno (PrivateEvents) sin cambios.
    // Issue #288: Descripcion (dato derivado) es irrelevante para este test -> placeholder "".
    private static readonly DetalleTurno DetalleTurnoTest = new(
        "Turno Manana",
        [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "");

    // Issue #322: TurnoDiario (ControlHoras.DomainEvents) -- lo que el handler persiste, mapeado
    // desde DetalleTurnoTest (mismos valores, tipo nuevo).
    private static readonly TurnoDiario TurnoDiarioTest = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "");

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PublicEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento() =>
        new(SolicitudId, EmpleadoDetalle, Fecha, DetalleTurnoTest);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado() =>
        new(StreamId, Empleado, Fecha, TurnoDiarioTest, SolicitudId);

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
        And<ControlDiarioAggregateRoot, Empleado?>(StreamId, c => c.InformacionEmpleado, Empleado);
        And<ControlDiarioAggregateRoot, DateOnly>(StreamId, c => c.Fecha, Fecha);
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, TurnoDiarioTest.Nombre);
    }

    // CA-4: YA existe ControlDiario para EmpleadoId+Fecha - el handler agrega al stream existente
    // CA-5: el nuevo evento contiene todos los campos actualizados
    // CA-8: el segundo mensaje opera sobre el mismo stream (mismo EmpleadoId+Fecha = mismo StreamId)
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_EmiteTurnoDiarioAsignado_CuandoYaExisteControlDiario()
    {
        var solicitudAnteriorId = Guid.Parse("019600b0-0000-7000-8000-000000000002");
        var turnoAnterior = new TurnoDiarioAsignado(StreamId, Empleado, Fecha, TurnoDiarioTest, solicitudAnteriorId);

        // Pre-carga el stream con el mismo StreamId que usara el handler (CA-8)
        Given(StreamId, turnoAnterior);
        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioAsignado());
        And<ControlDiarioAggregateRoot, Guid>(StreamId, c => c.UltimaSolicitudId, SolicitudId);
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, TurnoDiarioTest.Nombre);
    }

    // CA-4 (agregado en revision): el mapeo del Function App es recursivo hasta las sub-franjas,
    // pero los dos tests anteriores solo pasan franjas con Descansos/Extras vacios, asi que
    // MapearFranja/MapearSubFranja quedaban sin ejercitar. Aqui el evento privado entrante trae un
    // descanso y una extra con offsets distintos y Descripcion no vacia en los tres niveles: si el
    // mapeo perdiera un campo o cruzara DiaOffsetInicio/DiaOffsetFin, la comparacion
    // member-by-member de Then/And lo delata.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_MapeaDescansosYExtrasConSusOffsets_CuandoLaFranjaTraeSubFranjas()
    {
        var turnoEntrante = new DetalleTurno(
            "Turno Nocturno",
            [
                new DetalleFranjaOrdinaria(
                    new TimeOnly(22, 0), new TimeOnly(6, 0), 1,
                    [new DetalleSubFranja(new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)")],
                    [new DetalleSubFranja(new TimeOnly(6, 0), new TimeOnly(8, 0), 1, 1, "(06:00+1-08:00+1)")],
                    "(22:00-06:00+1)")
            ],
            "Turno Nocturno (22:00-06:00+1)");

        await WhenAsync(new ProgramacionTurnoDiarioSolicitada(
            SolicitudId, EmpleadoDetalle, Fecha, turnoEntrante));

        // Oraculo construido a mano, no derivado del mapeo bajo prueba (MEF-ADR-0002).
        var turnoPersistidoEsperado = new TurnoDiario(
            "Turno Nocturno",
            [
                new FranjaProgramada(
                    new TimeOnly(22, 0), new TimeOnly(6, 0), 1,
                    [new SubFranjaProgramada(new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)")],
                    [new SubFranjaProgramada(new TimeOnly(6, 0), new TimeOnly(8, 0), 1, 1, "(06:00+1-08:00+1)")],
                    "(22:00-06:00+1)")
            ],
            "Turno Nocturno (22:00-06:00+1)");

        Then(StreamId, new TurnoDiarioAsignado(
            StreamId, Empleado, Fecha, turnoPersistidoEsperado, SolicitudId));
        And<ControlDiarioAggregateRoot, TurnoDiario?>(
            StreamId, c => c.DetalleTurno, turnoPersistidoEsperado);
    }
}
