// Issue #183: Reemplazar el payload de DiaCalculado por HorasDiscriminadas plano
// CA-4: DiaCalculado expone HorasDiscriminadas y ya NO ControlesDeFranja ni el antiguo DesgloseHoras;
//       CrearDiaCalculado() lo construye via DesgloseHoras.Discriminar() sobre el desglose real del
//       aggregate (la propiedad DesgloseHoras recalculada por RecalcularDesgloseHoras()).
//
// Test directo sobre CrearDiaCalculado() (metodo publico del aggregate). Como ControlHoras NO expone
// InternalsVisibleTo, los factory internal (Iniciar/AsignarTurno/AdicionarMarcacion) no son accesibles
// desde el proyecto de tests: el aggregate se conduce al EventStore con los handlers reales (mismo
// patron que DesgloseHorasTras*Tests) y se verifica via el selector de And<>() sobre el aggregate
// rehidratado. And<>() compara estructuralmente (BeEquivalentTo).
//
// Nested classes: CrearDiaCalculado() lo conducen ambos EventHandlers sobre el mismo aggregate, por lo
// que comparten datos pero reaccionan a eventos privados distintos. Tras issue #209 y #210 ambos
// consumen su evento privado directo con PrivateEventHandlerAsyncTest<TEvent> (ADR-0024 #8):
// AsignarTurno reacciona a ProgramacionTurnoDiarioSolicitada; AdicionarMarcacion a MarcacionRegistrada.
//
// El valor esperado se registra A MANO como oraculo independiente: el diccionario de minutos por
// concepto se calcula de la geometria del dia, sin ejecutar Discriminar ni Consolidar (la logica bajo
// prueba). Asi un bug en esa logica no se filtra al esperado y el test si detecta regresiones (regla 20).

using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class CrearDiaCalculadoConHorasDiscriminadasTests
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

    // Marcaciones que completan la franja (entrada+salida) -> franja NO anomala (CA-4).
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(DetalleTurno detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    // Oraculo independiente: dia con franja 06:00-14:00 trabajada 07:00-15:00 (domingo 2026-03-15,
    // no anomala). El retardo 60min (06:00-07:00) se compensa con el excedente 60min (14:00-15:00) ->
    // retardo neto 0 (no hay clave "Retardo"); queda visible la ordinaria 07:00-14:00 = 420min
    // DominicalFestivaDiurna. Registrado a mano: ni Discriminar ni Consolidar se ejecutan para armarlo.
    private static IReadOnlyDictionary<string, int> MinutosFranjaCompleta() =>
        new Dictionary<string, int> { ["DominicalFestivaDiurna"] = 420 };

    public class ViaAsignarTurnoTests
        : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
    {
        protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
            new ProgramacionTurnoDiarioSolicitadaEventHandler(
                EventStore, PublicEventSender);

        private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
            new(SolicitudId, Empleado, Fecha, detalleTurno);

        // CA-4: con turno (franja 06:00-14:00) y marcaciones previas que la completan (07:00, 15:00),
        //       la franja queda NO anomala. CrearDiaCalculado() debe empaquetar el payload plano
        //       discriminado del desglose real: MinutosPorConcepto = {DominicalFestivaDiurna: 420}.
        // Issue #184: CrearDiaCalculado() ahora ademas puebla la Trazabilidad via Discriminar(). Un solo
        //       concepto (DominicalFestivaDiurna) y retardo neto 0 -> exactamente una linea. Se verifica
        //       solo el conteo (robusto ante como el desglose descomponga los intervalos del concepto);
        //       el contenido exacto de las lineas se prueba en DesgloseHorasDiscriminarTests, con geometria
        //       controlada. Aqui basta probar que el aggregate enruta la trazabilidad hacia el payload.
        [Fact]
        public async Task CrearDiaCalculado_LlevaMinutosYTrazabilidadReales_CuandoFranjaNoEsAnomala()
        {
            Given(StreamId,
                CrearMarcacionAdicionada(Timestamp07_00),
                CrearMarcacionAdicionada(Timestamp15_00));

            var turnoFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);
            await WhenAsync(CrearEvento(turnoFranjaUnica));

            Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

            And<ControlDiarioAggregateRoot, IReadOnlyDictionary<string, int>>(
                StreamId,
                c => c.CrearDiaCalculado().HorasDiscriminadas.MinutosPorConcepto,
                MinutosFranjaCompleta());

            And<ControlDiarioAggregateRoot, int>(
                StreamId,
                c => c.CrearDiaCalculado().HorasDiscriminadas.Trazabilidad.Count,
                1);
        }

        // CA-4 (todas las franjas anomalas): turno con franja 06:00-14:00 SIN marcaciones -> la franja
        //       queda anomala (sin entrada ni salida). El desglose real no aporta minutos calculables
        //       y no hay retardo, asi que el payload plano lleva MinutosPorConcepto vacio. El contrato
        //       plano ya no distingue "anomala" de "dia vacio" (riesgo aceptado del issue).
        [Fact]
        public async Task CrearDiaCalculado_LlevaMinutosPorConceptoVacio_CuandoTurnoSinMarcaciones()
        {
            // Sin Given - stream nuevo, turno sin marcaciones previas.
            var turnoFranjaUnica = new DetalleTurno("Turno Manana", [Franja06_14]);
            await WhenAsync(CrearEvento(turnoFranjaUnica));

            Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

            And<ControlDiarioAggregateRoot, HorasDiscriminadas>(
                StreamId,
                c => c.CrearDiaCalculado().HorasDiscriminadas,
                new HorasDiscriminadas(new Dictionary<string, int>(), []));
        }
    }

    public class ViaAdicionarMarcacionTests
        : PrivateEventHandlerAsyncTest<MarcacionRegistrada>
    {
        protected override IPrivateEventHandlerAsync<MarcacionRegistrada> Handler =>
            new MarcacionRegistradaEventHandler(
                EventStore, PublicEventSender);

        private static MarcacionRegistrada CrearMarcacionRegistrada(DateTime timestamp) =>
            new(Empleado.EmpleadoId, timestamp, "ENTRADA", "DEV-001");

        // CA-4 (sin turno): la marcacion crea el aggregate sin DetalleTurno -> Depurar() retorna lista
        //       vacia -> no hay ControlesDeFranja que consolidar. CrearDiaCalculado() empaqueta el
        //       payload plano con MinutosPorConcepto vacio.
        [Fact]
        public async Task CrearDiaCalculado_LlevaMinutosPorConceptoVacio_CuandoNoHayTurno()
        {
            // Sin Given - el aggregate nace solo con la marcacion (DetalleTurno queda null).
            await WhenAsync(CrearMarcacionRegistrada(Timestamp07_00));

            Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

            And<ControlDiarioAggregateRoot, HorasDiscriminadas>(
                StreamId,
                c => c.CrearDiaCalculado().HorasDiscriminadas,
                new HorasDiscriminadas(new Dictionary<string, int>(), []));
        }
    }
}
