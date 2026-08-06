// HU-123: Integrar depurador al ControlDiario de forma reactiva
// HU-131: Emitir DiaCalculado tras asignar turno (CA-1, CA-2)
// Familia 2: verifica que Apply(TurnoDiarioAsignado) dispara el recalculo de ControlesDeFranja
// y que el handler publica DiaCalculado via IPublicEventSender tras el recalculo.

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.ControlHoras;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class DepurarAlAsignarTurnoTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de EmpleadoId+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600c0-0000-7000-8000-000000000002");

    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    // Issue #318 CA-4: payload propio de PrivateEvents que llega en el evento bajo prueba;
    // el handler lo mapea a InformacionEmpleado. Paridad de campos con Empleado.
    private static readonly DetalleEmpleado EmpleadoDetalle = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{Empleado.EmpleadoId}:{Fecha:yyyy-MM-dd}";

    // CA-4: franja unica 06:00-14:00 para el turno asignado
    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
    private static readonly DetalleFranjaOrdinaria Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    // Timestamps de las marcaciones que llegan antes que el turno
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    // HU-131: handler ahora requiere IPublicEventSender para publicar DiaCalculado
    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PublicEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
        new(SolicitudId, EmpleadoDetalle, Fecha, detalleTurno);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    // Issue #184: oraculo independiente de una linea de trazabilidad (memoria de calculo). Se arma a
    // mano desde IntervaloTemporal.ToString() (primitiva ya probada) y la etiqueta traducida del recurso
    // (no un literal), sin ejecutar Discriminar (regla 20). Mismo patron que DesgloseHorasDiscriminarTests.
    private static string LineaConcepto(TimeOnly inicio, TimeOnly fin, Concepto concepto) =>
        $"{IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin))}: " +
        $"{IntervaloClasificado.Mensajes.Etiqueta(concepto)}";

    // CA-1 (HU-123): con 2 MarcacionAdicionada previas (07:00, 15:00) sin turno,
    //        al procesar ProgramacionTurnoDiarioSolicitada con franja 06:00-14:00,
    //        el Apply(TurnoDiarioAsignado) dispara Depurar() y ControlesDeFranja
    //        queda con ControlFranja(Franja06_14, Entrada=07:00, Salida=15:00).
    //        Verifica el caso "marcaciones llegaron antes que el turno".
    // CA-1 (HU-131): el handler publica DiaCalculado con InformacionEmpleado, Fecha
    //        y ControlesDeFranja actualizados (Entrada y Salida presentes, EsAnomala=false).
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_CalculaControlFranja_CuandoMarcacionesLlegaronAntesQueTurno()
    {
        var turnoConFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14], "");
        Given(StreamId,
            CrearMarcacionAdicionada(Timestamp07_00),
            CrearMarcacionAdicionada(Timestamp15_00));

        await WhenAsync(CrearEvento(turnoConFranjaUnica));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoConFranjaUnica));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, Timestamp15_00) });

        // HU-131 CA-1: publica DiaCalculado tras la depuracion. La franja 06:00-14:00 trabajada
        // 07:00-15:00 no es anomala. Issue #183 CA-4/CA-6: el payload viaja plano (MinutosPorConcepto),
        // sin ControlesDeFranja.
        // Esperado registrado A MANO con las primitivas del dominio (sin ejecutar Discriminar ni
        // Consolidar, la logica bajo prueba): en domingo 2026-03-15 el retardo 60min (06:00-07:00) se
        // compensa con el excedente 60min (14:00-15:00) -> retardo neto 0 (no hay clave "Retardo") y
        // queda visible la ordinaria 07:00-14:00 DominicalFestivaDiurna = 420min. Asi un bug en esa
        // logica no se filtra al esperado y el test lo detecta.
        // Issue #184: el DiaCalculado ahora viaja con la Trazabilidad (memoria de calculo) poblada. El
        // unico concepto del dia (ordinaria 07:00-14:00, DominicalFestivaDiurna) genera una sola linea,
        // construida con LineaConcepto desde el intervalo 07:00-14:00 y la etiqueta traducida del recurso.
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            new HorasDiscriminadas(
                new Dictionary<string, int> { ["DominicalFestivaDiurna"] = 420 },
                [LineaConcepto(new TimeOnly(7, 0), new TimeOnly(14, 0), Concepto.DominicalFestivaDiurna)])));
    }

    // CA-2 (HU-131): sin marcaciones previas, el Apply(TurnoDiarioAsignado) dispara Depurar()
    //        pero sin marcaciones el depurador retorna una franja con Entrada=null, Salida=null.
    //        DiaCalculado se emite aunque todos los ControlesDeFranja sean anomalos.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PublicaDiaCalculado_CuandoNoHayMarcacionesPrevias()
    {
        // Sin Given - stream nuevo, sin marcaciones previas
        var turnoConFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14], "");

        await WhenAsync(CrearEvento(turnoConFranjaUnica));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoConFranjaUnica));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, null, null) }); // EsAnomala=true

        // HU-131 CA-2: DiaCalculado se publica aunque la franja sea anomala (Entrada y Salida null).
        // Issue #183 CA-6: la franja anomala no aporta minutos calculables y no hay retardo, asi que
        // MinutosPorConcepto viaja vacio. El contrato plano no lleva senal de anomalia (riesgo aceptado).
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            new HorasDiscriminadas(new Dictionary<string, int>(), [])));
    }

    // CA-2 (HU-131): turno sin franjas ordinarias genera ControlesDeFranja vacio.
    //        DiaCalculado se emite igual con lista vacia de controles.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PublicaDiaCalculado_CuandoTurnoNoTieneFranjas()
    {
        // Sin Given - stream nuevo, turno sin franjas ordinarias
        var turnoSinFranjas = new DetalleTurno("Turno Sin Franjas", [], "");

        await WhenAsync(CrearEvento(turnoSinFranjas));

        Then(StreamId, CrearTurnoDiarioAsignado(turnoSinFranjas));
        And<ControlDiarioAggregateRoot, int>(
            StreamId,
            c => c.ControlesDeFranja.Count, 0);

        // HU-131 CA-2: DiaCalculado se publica aunque el turno no tenga franjas.
        // Issue #183 CA-6: sin franjas que consolidar ni retardo, MinutosPorConcepto viaja vacio.
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            new HorasDiscriminadas(new Dictionary<string, int>(), [])));
    }
}
