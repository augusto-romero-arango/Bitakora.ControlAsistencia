// HU-139: Integrar consolidador DesgloseHoras al flujo reactivo del ControlDiario
// Familia 2: verifica que Apply(TurnoDiarioAsignado) recalcula DesgloseHoras al final
// (despues de Depurar). Confirma que el hook reactivo tambien se dispara desde este Apply.
// Cubre CA-3 (turno que llega tras marcaciones previas -> consolidacion del dia) y
// CA-4 (turno sin marcaciones -> todas las franjas anomalas, FranjasAnomalas = numero de franjas).
//
// La matematica de consolidacion esta testeada en #116; aqui se verifica la INTEGRACION.
// El valor esperado se construye como oraculo independiente con las mismas primitivas del
// dominio que el hook reactivo invoca. And<>() compara estructuralmente (BeEquivalentTo).

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class DesgloseHorasTrasAsignarTurnoTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de EmpleadoId+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600c0-0000-7000-8000-000000000004");

    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{Empleado.EmpleadoId}:{Fecha:yyyy-MM-dd}";

    // CA-3: franja unica 06:00-14:00 para el turno asignado
    private static readonly DetalleFranjaOrdinaria Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], []);

    // CA-4: turno partido con dos franjas ordinarias (ambas quedaran anomalas sin marcaciones)
    private static readonly DetalleFranjaOrdinaria Franja06_12 =
        new(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], []);
    private static readonly DetalleFranjaOrdinaria Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], []);

    // Timestamps de las marcaciones que llegan antes que el turno (CA-3)
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PublicEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
        new(SolicitudId, Empleado, Fecha, detalleTurno);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    // CA-3: con 2 MarcacionAdicionada previas (07:00, 15:00) sin turno, al procesar
    //       ProgramacionTurnoDiarioSolicitada con franja 06:00-14:00 el Apply(TurnoDiarioAsignado)
    //       dispara Depurar() y luego RecalcularDesgloseHoras. DesgloseHoras refleja la
    //       consolidacion del dia usando el turno recien asignado (la franja queda no anomala).
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_RecalculaDesgloseHoras_CuandoTurnoLlegaTrasMarcaciones()
    {
        Given(StreamId,
            CrearMarcacionAdicionada(Timestamp07_00),
            CrearMarcacionAdicionada(Timestamp15_00));

        var turnoFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);
        await WhenAsync(CrearEvento(turnoFranjaUnica));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

        // Depuracion esperada: una franja con Entrada=07:00 y Salida=15:00 (no anomala).
        var controlFranja = new ControlFranja(Franja06_14, Timestamp07_00, Timestamp15_00);
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { controlFranja });

        // Oraculo independiente: el DesgloseHoras esperado se construye a mano con las primitivas del
        // dominio, SIN ejecutar Consolidar ni CalcularDesglose (la logica bajo prueba). Asi un cambio en
        // esa logica no se filtra al esperado y la prueba si detecta regresiones. Escenario: franja
        // 06:00-14:00 en domingo, trabajado 07:00-15:00 => retardo 60min (06:00-07:00) compensado por el
        // excedente 60min (14:00-15:00); queda ordinaria visible 07:00-14:00 DominicalFestivaDiurna (420min).
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

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            esperado);
    }

    // CA-4: solo TurnoDiarioAsignado sin ninguna marcacion -> todas las franjas quedan anomalas
    //       (sin entrada ni salida). RecalcularDesgloseHoras no consolida ningun DesgloseFranja,
    //       pero FranjasAnomalas refleja el numero de franjas ordinarias del turno (2).
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_DejaDesgloseHorasConFranjasAnomalas_CuandoTurnoSinMarcaciones()
    {
        // Sin Given - stream nuevo, turno partido sin marcaciones previas
        var turnoPartido = new DetalleTurno("Turno Partido", [Franja06_12, Franja14_18]);

        await WhenAsync(CrearEvento(turnoPartido));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoPartido));

        // FranjasAnomalas = 2 (las dos franjas ordinarias del turno, ambas sin entrada ni salida).
        And<ControlDiarioAggregateRoot, int>(
            StreamId,
            c => c.DesgloseHoras.FranjasAnomalas,
            2);

        // Sin franjas que consolidar: DesglosePorFranja vacio y RetardoTotal vacio, anomalas = 2.
        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            new DesgloseHoras([], Retardo.Vacio, 2));
    }
}
