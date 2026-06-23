// HU-139: Integrar consolidador DesgloseHoras al flujo reactivo del ControlDiario
// Familia 1: verifica que Apply(MarcacionAdicionada) recalcula DesgloseHoras al final
// (despues de Depurar), consolidando los ControlesDeFranja no anomalos del dia.
// Cubre CA-1 (consolidacion con marcaciones que completan el turno partido) y
// CA-2 (sin turno -> DesgloseHoras.Vacio porque no hay ControlesDeFranja que consolidar).
//
// La matematica de compensacion cross-franja esta testeada exhaustivamente en #116; aqui se
// verifica la INTEGRACION: que el hook se dispara y que el valor consolidado es coherente.
// El valor esperado se construye como oraculo independiente con las mismas primitivas del
// dominio que el hook reactivo invoca (#136 ControlFranja.CalcularDesglose, #116
// ConsolidadorDesgloseHoras.Consolidar). And<>() compara estructuralmente (BeEquivalentTo).

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

public class DesgloseHorasTrasAdicionarMarcacionTests : CommandHandlerAsyncTest<MarcacionRegistrada>
{
    // Datos de prueba fijos - mismo ancla de fecha y stream ID compuesto que los tests del handler
    private const string EmpleadoId = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{EmpleadoId}:{Fecha:yyyy-MM-dd}";

    private static readonly InformacionEmpleado Empleado = new(
        EmpleadoId, "CC", "1234567890", "Luis Augusto", "Barreto");
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000003");

    // CA-1: turno partido con dos franjas ordinarias 08:00-12:00 y 14:00-18:00
    private static readonly DetalleFranjaOrdinaria Franja08_12 =
        new(new TimeOnly(8, 0), new TimeOnly(12, 0), 0, [], []);
    private static readonly DetalleFranjaOrdinaria Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], []);

    // Timestamps de marcaciones (fuera de ventana nocturna: >= 04:00)
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp08_00 = new(2026, 3, 15, 8, 0, 0);
    private static readonly DateTime Timestamp12_05 = new(2026, 3, 15, 12, 5, 0);
    private static readonly DateTime Timestamp14_10 = new(2026, 3, 15, 14, 10, 0);
    private static readonly DateTime Timestamp18_30 = new(2026, 3, 15, 18, 30, 0);

    protected override ICommandHandlerAsync<MarcacionRegistrada> Handler =>
        new AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler(EventStore, PublicEventSender);

    private static MarcacionRegistrada CrearMarcacionRegistrada(DateTime timestamp) =>
        new(EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    // CA-1: con TurnoDiarioAsignado (turno partido 08:00-12:00 y 14:00-18:00) previo y
    //       MarcacionAdicionada previas (08:00, 12:05, 14:10), la MarcacionRegistrada a las 18:30
    //       completa la ultima franja y dispara el recalculo. DesgloseHoras refleja la consolidacion
    //       del dia con las dos franjas no anomalas (extras ajustadas por compensacion si aplica).
    [Fact]
    public async Task AdicionarMarcacion_RecalculaDesgloseHoras_CuandoMarcacionCompletaUltimaFranja()
    {
        var turnoPartido = new DetalleTurno("Turno Partido", [Franja08_12, Franja14_18]);
        Given(StreamId,
            CrearTurnoDiarioAsignado(turnoPartido),
            CrearMarcacionAdicionada(Timestamp08_00),
            CrearMarcacionAdicionada(Timestamp12_05),
            CrearMarcacionAdicionada(Timestamp14_10));

        await WhenAsync(CrearMarcacionRegistrada(Timestamp18_30));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp18_30));

        // Depuracion esperada: F1(08:00, 12:05) y F2(14:10, 18:30), ambas con entrada y salida.
        // Anclar los ControlesDeFranja documenta los insumos exactos del oraculo de DesgloseHoras.
        var controlFranja1 = new ControlFranja(Franja08_12, Timestamp08_00, Timestamp12_05);
        var controlFranja2 = new ControlFranja(Franja14_18, Timestamp14_10, Timestamp18_30);
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { controlFranja1, controlFranja2 });

        // Oraculo independiente: consolidar los desgloses de las dos franjas no anomalas (anomalas = 0).
        var esperado = ConsolidadorDesgloseHoras.Consolidar(
            new[]
            {
                controlFranja1.CalcularDesglose(Fecha, CalendarioFestivosColombia.EsFestivo)!,
                controlFranja2.CalcularDesglose(Fecha, CalendarioFestivosColombia.EsFestivo)!
            },
            franjasAnomalas: 0);

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            esperado);
    }

    // CA-2: sin turno previo (ningun Given), la MarcacionRegistrada crea el aggregate sin DetalleTurno.
    //       Depurar() retorna lista vacia -> RecalcularDesgloseHoras no tiene franjas que consolidar
    //       y DesgloseHoras queda en DesgloseHoras.Vacio.
    [Fact]
    public async Task AdicionarMarcacion_DejaDesgloseHorasVacio_CuandoNoHayTurnoPrevio()
    {
        // Sin Given - el aggregate nace solo con la marcacion (DetalleTurno queda null)
        await WhenAsync(CrearMarcacionRegistrada(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            DesgloseHoras.Vacio);
    }
}
