// HU-123: Integrar depurador al ControlDiario de forma reactiva
// HU-108: Emitir DiaCalculado tras adicionar marcacion (CA-1, CA-2, CA-3, CA-4, CA-5)
// Familia 1: verifica que Apply(MarcacionAdicionada) dispara el recalculo de ControlesDeFranja
// y que el handler publica DiaCalculado via IPublicEventSender tras cada recalculo.

using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoMarcacionRegistrada;

public class DepurarAlAdicionarMarcacionTests : CommandHandlerAsyncTest<MarcacionRegistrada>
{
    // Datos de prueba fijos - misma ancla de fecha que los tests del handler
    private const string EmpleadoId = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{EmpleadoId}:{Fecha:yyyy-MM-dd}";
    private static readonly string StreamIdDiaAnterior = $"{EmpleadoId}:2026-03-14";

    // Empleado para construir TurnoDiarioAsignado en Given
    private static readonly InformacionEmpleado Empleado = new(
        EmpleadoId, "CC", "1234567890", "Luis Augusto", "Barreto");
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000001");

    // CA-1: franja unica 06:00-14:00
    private static readonly DetalleFranjaOrdinaria Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], []);

    // CA-3: turno partido con dos franjas
    private static readonly DetalleFranjaOrdinaria Franja06_12 =
        new(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], []);
    private static readonly DetalleFranjaOrdinaria Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], []);

    // Timestamps de marcaciones (fuera de ventana nocturna: >= 04:00)
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp05_50 = new(2026, 3, 15, 5, 50, 0);
    private static readonly DateTime Timestamp12_05 = new(2026, 3, 15, 12, 5, 0);
    private static readonly DateTime Timestamp14_10 = new(2026, 3, 15, 14, 10, 0);

    // HU-108: handler ahora requiere IPublicEventSender para publicar DiaCalculado
    protected override ICommandHandlerAsync<MarcacionRegistrada> Handler =>
        new AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler(EventStore, PublicEventSender);

    private static MarcacionRegistrada CrearMarcacionRegistrada(DateTime timestamp) =>
        new(EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    // Issue #183: payload discriminado del dia sin conceptos ni retardo - el caso de franjas
    // anomalas (sin salida) o sin turno, donde el desglose discrimina a colecciones vacias.
    private static HorasDiscriminadas HorasDiscriminadasVacia() =>
        new(new Dictionary<string, int>(), []);

    // CA-1 (HU-123): con TurnoDiarioAsignado (franja unica 06:00-14:00) previo y MarcacionRegistrada a las 07:00,
    //        ControlesDeFranja debe quedar con un ControlFranja(Franja06_14, Entrada=07:00, Salida=null).
    //        Verifica que el hook reactivo se dispara desde Apply(MarcacionAdicionada).
    // CA-1 (HU-108): el handler publica DiaCalculado con InformacionEmpleado, Fecha y ControlesDeFranja correctos.
    [Fact]
    public async Task DebeCalcularControlFranja_CuandoHayTurnoYLlegaMarcacion()
    {
        var turnoUnico = new DetalleTurno("Turno Manana", [Franja06_14]);
        Given(StreamId, CrearTurnoDiarioAsignado(turnoUnico));

        await WhenAsync(CrearMarcacionRegistrada(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, null) });

        // Issue #183 CA-4: el handler publica DiaCalculado con HorasDiscriminadas (payload primitivo).
        // La unica franja es anomala (Salida null) -> no aporta conceptos y el retardo neto es 0;
        // MinutosPorConcepto y Trazabilidad quedan vacios. Oraculo independiente construido a mano.
        ThenIsPublishedPublicly(new DiaCalculado(
            Empleado,
            Fecha,
            HorasDiscriminadasVacia()));
    }

    // CA-2 (HU-123): sin turno previo, la marcacion crea el aggregate sin DetalleTurno.
    //        Depurar() retorna lista vacia porque DepuradorDeMarcaciones.Depurar(null,...) -> [].
    //        Verifica el caso "marcacion llega antes del turno" donde el aggregate
    //        se inicia solo con marcacion y no hay nada que depurar.
    // CA-2/CA-4 (HU-108): el handler publica DiaCalculado incluso cuando ControlesDeFranja esta vacio.
    [Fact]
    public async Task DebeDejarControlesDeFranjaVacios_CuandoNoHayTurnoPrevio()
    {
        // Sin Given - el aggregate se crea solo con la marcacion (DetalleTurno queda null)
        await WhenAsync(CrearMarcacionRegistrada(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);

        // Issue #183 CA-4: se publica DiaCalculado aunque no haya turno; HorasDiscriminadas queda vacio.
        // InformacionEmpleado es null porque el ControlDiario nacio solo por marcacion.
        ThenIsPublishedPublicly(new DiaCalculado(
            null,
            Fecha,
            HorasDiscriminadasVacia()));
    }

    // CA-3 (HU-123): con turno partido (06:00-12:00 y 14:00-18:00) y 2 MarcacionAdicionada previas
    //        (05:50 y 12:05), la nueva MarcacionRegistrada a las 14:10 dispara el recalculo
    //        completo: F1(Entrada=05:50, Salida=12:05) + F2(Entrada=14:10, Salida=null).
    //        Verifica comportamiento idem-potente: no acumula sobre resultado anterior.
    // Issue #183: este test conserva el foco en el recalculo de ControlesDeFranja; la verificacion
    //        del payload HorasDiscriminadas vive en los tests con oraculo independiente (ver abajo).
    [Fact]
    public async Task DebeRecalcularControlesDeFranjaCompletos_CuandoHayTurnoPartidoYMarcacionesAcumuladas()
    {
        var turnoPartido = new DetalleTurno("Turno Partido", [Franja06_12, Franja14_18]);
        Given(StreamId,
            CrearTurnoDiarioAsignado(turnoPartido),
            CrearMarcacionAdicionada(Timestamp05_50),
            CrearMarcacionAdicionada(Timestamp12_05));

        await WhenAsync(CrearMarcacionRegistrada(Timestamp14_10));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp14_10));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[]
            {
                new(Franja06_12, Timestamp05_50, Timestamp12_05),
                new(Franja14_18, Timestamp14_10, null)
            });

        // Issue #183: este escenario tiene una franja completa NO anomala en domingo (2026-03-15),
        // cuyo desglose discriminado (DominicalFestiva/Extra con compensacion cross-franja) no es
        // construible a mano sin replicar la logica bajo prueba (regla absoluta 20: oraculo
        // independiente). La correctitud de HorasDiscriminadas se verifica donde el oraculo SI es
        // independiente: DesgloseHorasDiscriminarTests (la discriminacion con insumos hechos a mano)
        // y DiaCalculadoSerializacionTests (el roundtrip del payload). El foco de este test es el
        // recalculo idempotente de ControlesDeFranja, ya verificado arriba con And<>().
    }

    // CA-5 (HU-108): marcacion a las 02:00 cae en ventana nocturna [00:00, 04:00).
    // El handler procesa dos fechas-destino: dia calendario (2026-03-15) y dia anterior (2026-03-14).
    // Publica dos DiaCalculado - uno por cada fecha, en el orden de procesamiento del handler.
    // Verifica con ThenIsPublishedPublicly(evento1, evento2): count exacto + orden exacto.
    [Fact]
    public async Task AdicionarMarcacion_PublicaDosDiaCalculado_CuandoMarcacionEstaEnVentanaNocturna()
    {
        // Sin Given - ninguno de los dos streams existe
        var timestampNocturno = new DateTime(2026, 3, 15, 2, 0, 0);
        var fechaDiaCal = Fecha;                        // 2026-03-15 (dia calendario)
        var fechaDiaAnt = Fecha.AddDays(-1);            // 2026-03-14 (dia anterior)

        await WhenAsync(CrearMarcacionRegistrada(timestampNocturno));

        // Verificar que cada stream tiene exactamente un MarcacionAdicionada
        Then(StreamId,
            new MarcacionAdicionada(StreamId, EmpleadoId, timestampNocturno, "ENTRADA", "DEV-001"));
        Then(StreamIdDiaAnterior,
            new MarcacionAdicionada(StreamIdDiaAnterior, EmpleadoId, timestampNocturno, "ENTRADA", "DEV-001"));

        // CA-5: dos DiaCalculado publicados, en orden: dia calendario primero, dia anterior segundo.
        // Ambos sin turno previo (InformacionEmpleado=null, HorasDiscriminadas vacio).
        ThenIsPublishedPublicly(
            new DiaCalculado(null, fechaDiaCal, HorasDiscriminadasVacia()),
            new DiaCalculado(null, fechaDiaAnt, HorasDiscriminadasVacia()));

        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDiaAnterior, c => c.ControlesDeFranja.Count, 0);
    }
}
