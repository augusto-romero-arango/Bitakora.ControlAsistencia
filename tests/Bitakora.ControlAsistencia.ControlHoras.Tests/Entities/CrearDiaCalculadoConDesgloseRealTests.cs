// HU-181: Publicar el DesgloseHoras real en el evento DiaCalculado
// CA-1/CA-2: CrearDiaCalculado() empaqueta el DesgloseHoras REAL recalculado del aggregate
//            (la propiedad DesgloseHoras), no DesgloseHoras.Vacio.
//
// Test directo sobre CrearDiaCalculado() (metodo publico del aggregate). Como ControlHoras
// NO expone InternalsVisibleTo, los factory internal (Iniciar/AsignarTurno/AdicionarMarcacion)
// no son accesibles desde el proyecto de tests: el aggregate se conduce al EventStore con los
// handlers reales (mismo patron que DesgloseHorasTras*Tests) y se verifica via el selector de
// And<>() sobre el aggregate rehidratado. And<>() compara estructuralmente (BeEquivalentTo).
//
// Nested classes: CrearDiaCalculado() lo invocan ambos handlers (AsignarTurno y AdicionarMarcacion)
// sobre el mismo aggregate, por lo que comparten datos pero requieren bases CommandHandlerAsyncTest
// distintas (una por tipo de comando).
//
// El valor esperado se construye como oraculo independiente con las primitivas del dominio
// (ConsolidadorDesgloseHoras.Consolidar + ControlFranja.CalcularDesglose); la matematica de
// consolidacion esta testeada exhaustivamente en #116/#136/#139. Aqui se verifica el "ultimo
// centimetro": que el evento lleve ese valor y no DesgloseHoras.Vacio.

using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class CrearDiaCalculadoConDesgloseRealTests
{
    // Datos compartidos - el stream ID es determinista a partir de EmpleadoId+Fecha.
    // private static: las nested classes acceden a los miembros privados de la clase contenedora.
    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{Empleado.EmpleadoId}:{Fecha:yyyy-MM-dd}";
    private static readonly Guid SolicitudId = Guid.Parse("019600d0-0000-7000-8000-000000000001");

    // Franja unica 06:00-14:00 usada por los escenarios de turno.
    private static readonly DetalleFranjaOrdinaria Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], []);

    // Marcaciones que completan la franja (entrada+salida) -> franja NO anomala (CA-1).
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    // Oraculo independiente: el DesgloseHoras real de un dia con la franja 06:00-14:00 trabajada
    // 07:00-15:00 (no anomala). Se construye con las primitivas del dominio, igual que el hook
    // reactivo del aggregate (RecalcularDesgloseHoras).
    private static DesgloseHoras DesgloseRealFranjaCompleta() =>
        ConsolidadorDesgloseHoras.Consolidar(
            new[]
            {
                new ControlFranja(Franja06_14, Timestamp07_00, Timestamp15_00)
                    .CalcularDesglose(Fecha, CalendarioFestivosColombia.EsFestivo)!
            },
            franjasAnomalas: 0);

    public class ViaAsignarTurnoTests
        : CommandHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
    {
        protected override ICommandHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
            new AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler(
                EventStore, PublicEventSender);

        private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
            new(SolicitudId, Empleado, Fecha, detalleTurno);

        // CA-1: con turno (franja 06:00-14:00) y marcaciones previas que la completan (07:00, 15:00),
        //       la franja queda NO anomala. CrearDiaCalculado() debe empaquetar el DesgloseHoras real
        //       consolidado del dia (no DesgloseHoras.Vacio), exactamente el mismo valor que expone la
        //       propiedad DesgloseHoras del aggregate.
        [Fact]
        public async Task CrearDiaCalculado_LlevaDesgloseRealIgualAlAggregate_CuandoFranjaNoEsAnomala()
        {
            Given(StreamId,
                CrearMarcacionAdicionada(Timestamp07_00),
                CrearMarcacionAdicionada(Timestamp15_00));

            var turnoFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);
            await WhenAsync(CrearEvento(turnoFranjaUnica));

            Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

            var desgloseReal = DesgloseRealFranjaCompleta();

            // El evento empaquetado por CrearDiaCalculado() lleva el desglose real (oraculo no vacio
            // => no es DesgloseHoras.Vacio).
            And<ControlDiarioAggregateRoot, DesgloseHoras>(
                StreamId,
                c => c.CrearDiaCalculado().DesgloseHoras,
                desgloseReal);

            // ...y coincide con la propiedad DesgloseHoras del aggregate (mismo valor consolidado):
            // ambos equivalen al mismo oraculo, luego el evento refleja el estado del aggregate.
            And<ControlDiarioAggregateRoot, DesgloseHoras>(
                StreamId,
                c => c.DesgloseHoras,
                desgloseReal);
        }

        // CA-2 (todas las franjas anomalas): turno con franja 06:00-14:00 SIN marcaciones -> la franja
        //       queda anomala (sin entrada ni salida). DesglosePorFranja vacio y RetardoTotal vacio,
        //       pero FranjasAnomalas refleja el conteo correcto (1). NO es DesgloseHoras.Vacio, que
        //       tiene FranjasAnomalas = 0.
        [Fact]
        public async Task CrearDiaCalculado_LlevaDesgloseConFranjasAnomalas_CuandoTurnoSinMarcaciones()
        {
            // Sin Given - stream nuevo, turno sin marcaciones previas.
            var turnoFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);
            await WhenAsync(CrearEvento(turnoFranjaUnica));

            Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

            And<ControlDiarioAggregateRoot, DesgloseHoras>(
                StreamId,
                c => c.CrearDiaCalculado().DesgloseHoras,
                new DesgloseHoras([], DetalleRetardo.Vacio, 1));

            // Explicito sobre el conteo de anomalas que distingue este caso de DesgloseHoras.Vacio (FA=0).
            And<ControlDiarioAggregateRoot, int>(
                StreamId,
                c => c.CrearDiaCalculado().DesgloseHoras.FranjasAnomalas,
                1);
        }
    }

    public class ViaAdicionarMarcacionTests
        : CommandHandlerAsyncTest<MarcacionRegistrada>
    {
        protected override ICommandHandlerAsync<MarcacionRegistrada> Handler =>
            new AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler(
                EventStore, PublicEventSender);

        private static MarcacionRegistrada CrearMarcacionRegistrada(DateTime timestamp) =>
            new(Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

        // CA-2 (sin turno): la marcacion crea el aggregate sin DetalleTurno -> Depurar() retorna lista
        //       vacia -> no hay ControlesDeFranja que consolidar. CrearDiaCalculado() debe seguir
        //       empaquetando DesgloseHoras.Vacio (se preserva el comportamiento del caso vacio).
        [Fact]
        public async Task CrearDiaCalculado_LlevaDesgloseVacio_CuandoNoHayTurno()
        {
            // Sin Given - el aggregate nace solo con la marcacion (DetalleTurno queda null).
            await WhenAsync(CrearMarcacionRegistrada(Timestamp07_00));

            Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

            And<ControlDiarioAggregateRoot, DesgloseHoras>(
                StreamId,
                c => c.CrearDiaCalculado().DesgloseHoras,
                DesgloseHoras.Vacio);
        }
    }
}
