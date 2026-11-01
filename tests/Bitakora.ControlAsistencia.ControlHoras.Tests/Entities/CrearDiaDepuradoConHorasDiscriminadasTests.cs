// CrearDiaDepurado() empaqueta el payload plano via DesgloseHoras.Discriminar() sobre el desglose
// real del aggregate (la propiedad DesgloseHoras que RecalcularDesgloseHoras() refresca en cada Apply).
//
// Test directo sobre CrearDiaDepurado() (metodo publico del aggregate). Como ControlHoras NO expone
// InternalsVisibleTo, los factory internal (Iniciar/AsignarTurno/AdicionarMarcacion) no son accesibles
// desde el proyecto de tests: el aggregate se conduce al EventStore con los handlers reales (mismo
// patron que DesgloseHorasTras*Tests) y se verifica via el selector de And<>() sobre el aggregate
// rehidratado. And<>() compara estructuralmente (BeEquivalentTo).
//
// Nested classes: CrearDiaDepurado() lo conducen ambos EventHandlers sobre el mismo aggregate, por lo
// que comparten datos pero reaccionan a eventos privados distintos. Tras issue #209 y #210 ambos
// consumen su evento privado directo con PrivateEventHandlerAsyncTest<TEvent> (ADR-0024 #8):
// AsignarTurnoEnLote reacciona a ProgramacionTurnoEnFechasSolicitada; AdicionarMarcacion a
// RegistroDeMarcacionCreado (issue #270: reemplaza a MarcacionRegistrada, que dejo de implementar
// IPrivateEvent -- CA-3).
//
// El valor esperado se registra A MANO como oraculo independiente: el diccionario de horas por
// concepto se calcula de la geometria del dia, sin ejecutar Discriminar ni Consolidar ni
// HorasLiquidables.DesdeMinutos (la logica bajo prueba). Asi un bug en esa logica no se filtra al
// esperado y el test si detecta regresiones (regla 20).

using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
// Esta isla declara su propio ResumenColaborador: el alias fija cual de los dos homonimos
// es el que trae el evento privado (CS0104).
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
// Alias de tipo: estos nombres existen homonimos en ControlHoras.DomainEvents (payload por rol,
// MEF-ADR-0039 decision #6); este archivo usa los del bus.
using FranjaDepurada = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.FranjaDepurada;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;
using RegistroDeMarcacionCreado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.RegistroDeMarcacionCreado;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class CrearDiaDepuradoConHorasDiscriminadasTests
{
    // Datos compartidos - el stream ID es determinista a partir de CodigoColaborador+Fecha.
    // private static: las nested classes acceden a los miembros privados de la clase contenedora.
    // ColaboradorProgramado (ControlHoras.DomainEvents) es el tipo que persiste TurnoDiarioAsignado.
    private static readonly ColaboradorProgramado Colaborador = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    // Mismo colaborador en la forma de bus; el handler lo mapea campo a campo al que persiste
    // TurnoDiarioAsignado. Los dos literales se mantienen separados para que una permutacion de
    // campos en ese mapeo entre islas se delate aqui.
    private static readonly ResumenColaborador ColaboradorResumen = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";
    private static readonly Guid SolicitudId = Guid.Parse("019600d0-0000-7000-8000-000000000001");

    // Franja unica 06:00-14:00 usada por los escenarios de turno. FranjaProgramada
    // (ControlHoras.DomainEvents) es lo que el ControlDiario persiste; DetalleFranjaOrdinaria
    // (PrivateEvents) es lo que trae el evento privado entrante.
    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
    private static readonly FranjaProgramada Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");
    private static readonly DetalleFranjaOrdinaria Franja06_14Detalle =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    // Gemelo con sede de la franja anterior. Los literales de DetalleSede (bus entrante) y
    // SedeProgramada (persistida) se mantienen separados a proposito: una permutacion de campos en
    // el mapeo entre islas se delata aqui (mismo criterio que Colaborador/ColaboradorResumen).
    private static readonly DetalleSede SedeDetalle = new("001", "Sede Principal", "CC-100");
    private static readonly SedeProgramada SedeEsperada = new("001", "Sede Principal", "CC-100");
    private static readonly DetalleFranjaOrdinaria Franja06_14ConSedeDetalle =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "", SedeDetalle);
    private static readonly FranjaProgramada Franja06_14ConSede =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "", SedeEsperada);

    // Marcaciones que completan la franja (entrada+salida) -> franja NO anomala (CA-4).
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario turnoDiario) =>
        new(StreamId, Colaborador, Fecha, turnoDiario, SolicitudId);

    // Oraculo independiente: dia con franja 06:00-14:00 trabajada 07:00-15:00 (domingo 2026-03-15,
    // no anomala). El retardo 60min (06:00-07:00) se compensa con el excedente 60min (14:00-15:00) ->
    // retardo neto 0 (no hay clave "Retardo"); queda visible la ordinaria 07:00-14:00 = 420min
    // DominicalFestivaDiurna = 7.00 horas liquidables (420/60). Registrado a mano: ni Discriminar, ni
    // Consolidar, ni HorasLiquidables.DesdeMinutos se ejecutan para armarlo.
    private static IReadOnlyDictionary<string, decimal> HorasFranjaCompleta() =>
        new Dictionary<string, decimal> { ["DominicalFestivaDiurna"] = 7.00m };

    public class ViaAsignarTurnoEnLoteTests
        : PrivateEventHandlerAsyncTest<ProgramacionTurnoEnFechasSolicitada>
    {
        protected override IPrivateEventHandlerAsync<ProgramacionTurnoEnFechasSolicitada> Handler =>
            new ProgramacionTurnoEnFechasSolicitadaEventHandler(EventStore, PrivateEventSender);

        private static ProgramacionTurnoEnFechasSolicitada CrearEvento(DetalleTurno detalleTurno) =>
            new(SolicitudId, ColaboradorResumen, [Fecha], detalleTurno, null!);

        [Fact]
        public async Task CrearDiaDepurado_LlevaHorasYTrazabilidadReales_CuandoFranjaNoEsAnomala()
        {
            Given(StreamId,
                CrearMarcacionAdicionada(Timestamp07_00),
                CrearMarcacionAdicionada(Timestamp15_00));

            await WhenAsync(CrearEvento(new DetalleTurno("Turno Manana", [Franja06_14Detalle], "")));

            Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14], "")));

            And<ControlDiarioAggregateRoot, IReadOnlyDictionary<string, decimal>>(
                StreamId,
                c => c.CrearDiaDepurado().HorasDiscriminadas.HorasPorConcepto,
                HorasFranjaCompleta());

            And<ControlDiarioAggregateRoot, int>(
                StreamId,
                c => c.CrearDiaDepurado().HorasDiscriminadas.Trazabilidad.Count,
                1);

            And<ControlDiarioAggregateRoot, string?>(
                StreamId,
                c => c.CrearDiaDepurado().NombreTurno,
                "Turno Manana");

            And<ControlDiarioAggregateRoot, IReadOnlyList<FranjaDepurada>>(
                StreamId,
                c => c.CrearDiaDepurado().Franjas,
                [new FranjaDepurada(
                    new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                    Timestamp07_00, Timestamp15_00, false)]);

            And<ControlDiarioAggregateRoot, IReadOnlyList<MarcacionDelDia>>(
                StreamId,
                c => c.CrearDiaDepurado().Marcaciones,
                [new MarcacionDelDia(Timestamp07_00, "ENTRADA", Pertenencia: "DelTurno"), new MarcacionDelDia(Timestamp15_00, "ENTRADA", Pertenencia: "DelTurno")]);
        }

        [Fact]
        public async Task CrearDiaDepurado_OrdenaMarcacionesCronologicamente_CuandoLlegaronDesordenadas()
        {
            Given(StreamId,
                CrearMarcacionAdicionada(Timestamp15_00),
                CrearMarcacionAdicionada(Timestamp07_00));

            await WhenAsync(CrearEvento(new DetalleTurno("Turno Manana", [Franja06_14Detalle], "")));

            Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14], "")));

            And<ControlDiarioAggregateRoot, string>(
                StreamId,
                c => string.Join(
                    "|",
                    c.CrearDiaDepurado().Marcaciones.Select(m => m.Timestamp.ToString("HH:mm"))),
                "07:00|15:00");
        }

        [Fact]
        public async Task CrearDiaDepurado_LlevaHorasPorConceptoVacio_CuandoTurnoSinMarcaciones()
        {
            await WhenAsync(CrearEvento(new DetalleTurno("Turno Manana", [Franja06_14Detalle], "")));

            Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14], "")));

            And<ControlDiarioAggregateRoot, HorasDiscriminadas>(
                StreamId,
                c => c.CrearDiaDepurado().HorasDiscriminadas,
                new HorasDiscriminadas(new Dictionary<string, decimal>(), []));

            And<ControlDiarioAggregateRoot, IReadOnlyList<FranjaDepurada>>(
                StreamId,
                c => c.CrearDiaDepurado().Franjas,
                [new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, null, null, true)]);

            And<ControlDiarioAggregateRoot, IReadOnlyList<MarcacionDelDia>>(
                StreamId,
                c => c.CrearDiaDepurado().Marcaciones,
                []);
        }

        [Fact]
        public async Task CrearDiaDepurado_LlevaSedeProgramadaEnLaFranja_CuandoLaFranjaTraeSedeConCentroDeCostos()
        {
            Given(StreamId,
                CrearMarcacionAdicionada(Timestamp07_00),
                CrearMarcacionAdicionada(Timestamp15_00));

            await WhenAsync(CrearEvento(new DetalleTurno("Turno Manana", [Franja06_14ConSedeDetalle], "")));

            Then(StreamId, CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14ConSede], "")));

            And<ControlDiarioAggregateRoot, IReadOnlyList<FranjaDepurada>>(
                StreamId,
                c => c.CrearDiaDepurado().Franjas,
                [new FranjaDepurada(
                    new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                    Timestamp07_00, Timestamp15_00, false,
                    "001", "Sede Principal", "CC-100")]);
        }
    }

    public class ViaAdicionarMarcacionTests
        : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
    {
        protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
            new RegistroDeMarcacionCreadoEventHandler(
                EventStore, PrivateEventSender);

        private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(DateTime timestamp) =>
            new(Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

        // CA-4/CA-6 (sin turno): la marcacion crea el aggregate sin DetalleTurno -> Depurar() retorna
        //       lista vacia -> no hay ControlesDeFranja que consolidar. CrearDiaDepurado() empaqueta el
        //       payload plano con HorasPorConcepto vacio, NombreTurno null y Franjas vacia (dia sin
        //       jornada valida); Marcaciones lleva la marcacion cruda.
        [Fact]
        public async Task CrearDiaDepurado_LlevaHorasPorConceptoVacio_CuandoNoHayTurno()
        {
            // Sin Given - el aggregate nace solo con la marcacion (DetalleTurno queda null).
            await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

            Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

            And<ControlDiarioAggregateRoot, HorasDiscriminadas>(
                StreamId,
                c => c.CrearDiaDepurado().HorasDiscriminadas,
                new HorasDiscriminadas(new Dictionary<string, decimal>(), []));

            And<ControlDiarioAggregateRoot, string?>(
                StreamId,
                c => c.CrearDiaDepurado().NombreTurno,
                null);

            And<ControlDiarioAggregateRoot, IReadOnlyList<MarcacionDelDia>>(
                StreamId,
                c => c.CrearDiaDepurado().Marcaciones,
                [new MarcacionDelDia(Timestamp07_00, "ENTRADA")]);
        }

        // Issue #1107 (CA-9): con ventana de entrada, FranjaDepurada reporta el horario efectivo (la
        //       franja desplazada), no el de referencia. Turno 07:00-16:00 con amplitud 180 y entrada
        //       10:20 -> el desplazamiento se acota al cierre de la ventana: 10:00-19:00.
        [Fact]
        public async Task CrearDiaDepurado_ReportaElHorarioEfectivo_CuandoTurnoTieneVentanaDeEntrada()
        {
            var entrada = new DateTime(2026, 3, 15, 10, 20, 0);
            var salida = new DateTime(2026, 3, 15, 19, 0, 0);
            var turnoConVentana = new TurnoDiario(
                "Turno Ventana",
                [new FranjaProgramada(new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "")],
                "",
                AmplitudVentanaEntradaEnMinutos: 180);
            Given(StreamId,
                CrearTurnoDiarioAsignado(turnoConVentana),
                CrearMarcacionAdicionada(entrada));

            await WhenAsync(CrearRegistroDeMarcacionCreado(salida));

            Then(StreamId, CrearMarcacionAdicionada(salida));

            And<ControlDiarioAggregateRoot, IReadOnlyList<FranjaDepurada>>(
                StreamId,
                c => c.CrearDiaDepurado().Franjas,
                [new FranjaDepurada(new TimeOnly(10, 0), new TimeOnly(19, 0), 0, entrada, salida, false)]);
        }

        // CA-4: sin turno, Colaborador queda null y CodigoColaborador solo puede salir del stream ID.
        //       El consumidor (#425) arma "dc:{codigo}:{yyyyMMdd}" con el, asi que nunca puede faltar.
        [Fact]
        public async Task CrearDiaDepurado_LlevaCodigoColaboradorTopLevel_CuandoNoHayTurno()
        {
            await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

            Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

            And<ControlDiarioAggregateRoot, string>(
                StreamId,
                c => c.CrearDiaDepurado().CodigoColaborador,
                Colaborador.CodigoColaborador);
        }
    }
}
