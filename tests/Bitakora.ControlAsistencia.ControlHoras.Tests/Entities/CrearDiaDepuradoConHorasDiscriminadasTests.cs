// CrearDiaDepurado() empaqueta el payload plano via DesgloseHoras.Discriminar() sobre el desglose
// real del aggregate (la propiedad DesgloseHoras que RecalcularDesgloseHoras() refresca en cada Apply),
// enriquecido con NombreTurno (DetalleTurno?.Nombre), Franjas (espejo de ControlesDeFranja) y
// Marcaciones (todas, en orden cronologico ascendente) -- issue #424.
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
// AsignarTurno reacciona a ProgramacionTurnoDiarioSolicitada; AdicionarMarcacion a
// RegistroDeMarcacionCreado (issue #270: reemplaza a MarcacionRegistrada, que dejo de implementar
// IPrivateEvent -- CA-3).
//
// El valor esperado se registra A MANO como oraculo independiente: el diccionario de horas por
// concepto se calcula de la geometria del dia, sin ejecutar Discriminar ni Consolidar ni
// HorasLiquidables.DesdeMinutos (la logica bajo prueba). Asi un bug en esa logica no se filtra al
// esperado y el test si detecta regresiones (regla 20).

using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class CrearDiaDepuradoConHorasDiscriminadasTests
{
    // Datos compartidos - el stream ID es determinista a partir de CodigoColaborador+Fecha.
    // private static: las nested classes acceden a los miembros privados de la clase contenedora.
    // Issue #322: Colaborador (ControlHoras.DomainEvents) -- el tipo que persiste TurnoDiarioAsignado.
    private static readonly ColaboradorProgramado Colaborador = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    // Mismo colaborador, en la forma con que llega dentro del evento privado; el handler lo mapea
    // a Colaborador para TurnoDiarioAsignado (CA-ADR-0029 decision #5).
    private static readonly DetalleColaborador ColaboradorDetalle = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

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

    public class ViaAsignarTurnoTests
        : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
    {
        protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
            new ProgramacionTurnoDiarioSolicitadaEventHandler(
                EventStore, PrivateEventSender);

        private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
            new(SolicitudId, ColaboradorDetalle, Fecha, detalleTurno);

        // CA-4: con turno (franja 06:00-14:00) y marcaciones previas que la completan (07:00, 15:00),
        //       la franja queda NO anomala. CrearDiaDepurado() debe empaquetar el payload plano
        //       discriminado del desglose real: HorasPorConcepto = {DominicalFestivaDiurna: 7.00}.
        // Issue #184: CrearDiaDepurado() ademas puebla la Trazabilidad via Discriminar(). Un solo
        //       concepto (DominicalFestivaDiurna) y retardo neto 0 -> exactamente una linea. Se verifica
        //       solo el conteo (robusto ante como el desglose descomponga los intervalos del concepto);
        //       el contenido exacto de las lineas se prueba en DesgloseHorasDiscriminarTests, con geometria
        //       controlada. Aqui basta probar que el aggregate enruta la trazabilidad hacia el payload.
        // Issue #424 (CA-3/CA-5): Franjas lleva el espejo del ControlFranja real y NombreTurno la senal
        //       estructural del plan (nombre presente + Franjas >= 1 -> jornada valida).
        [Fact]
        public async Task CrearDiaDepurado_LlevaHorasYTrazabilidadReales_CuandoFranjaNoEsAnomala()
        {
            Given(StreamId,
                CrearMarcacionAdicionada(Timestamp07_00),
                CrearMarcacionAdicionada(Timestamp15_00));

            var turnoFranjaUnicaDetalle = new DetalleTurno("Turno Manana", [Franja06_14Detalle], "");
            await WhenAsync(CrearEvento(turnoFranjaUnicaDetalle));

            var turnoFranjaUnica = new TurnoDiario("Turno Manana", [Franja06_14], "");
            Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

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
                [new MarcacionDelDia(Timestamp07_00, "ENTRADA"), new MarcacionDelDia(Timestamp15_00, "ENTRADA")]);
        }

        // CA-4 (todas las franjas anomalas)/CA-6: turno con franja 06:00-14:00 SIN marcaciones -> la
        //       franja queda anomala (sin entrada ni salida). El desglose real no aporta horas
        //       calculables y no hay retardo, asi que HorasPorConcepto viaja vacio. Franjas SI lleva la
        //       franja anomala (espejo de ControlesDeFranja); Marcaciones queda vacia.
        [Fact]
        public async Task CrearDiaDepurado_LlevaHorasPorConceptoVacio_CuandoTurnoSinMarcaciones()
        {
            // Sin Given - stream nuevo, turno sin marcaciones previas.
            var turnoFranjaUnicaDetalle = new DetalleTurno("Turno Manana", [Franja06_14Detalle], "");
            await WhenAsync(CrearEvento(turnoFranjaUnicaDetalle));

            var turnoFranjaUnica = new TurnoDiario("Turno Manana", [Franja06_14], "");
            Then(StreamId, CrearTurnoDiarioAsignado(turnoFranjaUnica));

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
