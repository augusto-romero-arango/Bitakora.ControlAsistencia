// HU-123: Integrar depurador al ControlDiario de forma reactiva
// Familia 1: verifica que Apply(MarcacionAdicionada) dispara el recalculo de ControlesDeFranja

using AwesomeAssertions;
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

    protected override ICommandHandlerAsync<MarcacionRegistrada> Handler =>
        new AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler(EventStore);

    private static MarcacionRegistrada CrearMarcacionRegistrada(DateTime timestamp) =>
        new(EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    // CA-1: con TurnoDiarioAsignado (franja unica 06:00-14:00) previo y MarcacionRegistrada a las 07:00,
    //        ControlesDeFranja debe quedar con un ControlFranja(Franja06_14, Entrada=07:00, Salida=null).
    //        Verifica que el hook reactivo se dispara desde Apply(MarcacionAdicionada).
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
    }

    // CA-2: sin turno previo, la marcacion crea el aggregate sin DetalleTurno.
    //        Depurar() retorna lista vacia porque DepuradorDeMarcaciones.Depurar(null,...) -> [].
    //        Verifica el caso "marcacion llega antes del turno" donde el aggregate
    //        se inicia solo con marcacion y no hay nada que depurar.
    [Fact]
    public async Task DebeDejarControlesDeFranjaVacios_CuandoNoHayTurnoPrevio()
    {
        // Sin Given - el aggregate se crea solo con la marcacion (DetalleTurno queda null)
        await WhenAsync(CrearMarcacionRegistrada(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);
    }

    // CA-3: con turno partido (06:00-12:00 y 14:00-18:00) y 2 MarcacionAdicionada previas
    //        (05:50 y 12:05), la nueva MarcacionRegistrada a las 14:10 dispara el recalculo
    //        completo: F1(Entrada=05:50, Salida=12:05) + F2(Entrada=14:10, Salida=null).
    //        Verifica comportamiento idem-potente: no acumula sobre resultado anterior.
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
    }
}
