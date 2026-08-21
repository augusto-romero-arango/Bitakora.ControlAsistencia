// HU-123: Integrar depurador al ControlDiario de forma reactiva
// HU-131: Emitir DiaDepurado tras asignar turno (CA-1, CA-2)
// Familia 2: verifica que Apply(TurnoDiarioAsignado) dispara el recalculo de ControlesDeFranja
// y que el handler publica DiaDepurado via IPrivateEventSender tras el recalculo.
// El tercer test (turno con cero franjas) construye en unit un caso que hoy no ocurre en runtime:
// el catalogo todavia no produce turnos sin franjas. No borrarlo por "imposible" -- congela el
// contrato del descanso programado (nombre de turno presente + cero franjas).

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class DepurarAlAsignarTurnoTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de CodigoColaborador+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600c0-0000-7000-8000-000000000002");

    // Issue #322: Colaborador (ControlHoras.DomainEvents) -- el tipo que persiste TurnoDiarioAsignado.
    private static readonly ColaboradorProgramado Colaborador = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    // Oraculo a mano de la composicion que hace CrearResumenColaborador(): Identificacion
    // "{Tipo}-{Numero}" y NombreCompleto "{Nombres} {Apellidos}" de Colaborador.
    private static readonly ResumenColaborador ColaboradorResumen = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    // Mismo colaborador, en la forma con que llega dentro del evento privado; el handler lo mapea
    // a Colaborador para TurnoDiarioAsignado (CA-ADR-0029 decision #5).
    private static readonly DetalleColaborador ColaboradorDetalle = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    // CA-4: franja unica 06:00-14:00 para el turno asignado -- FranjaProgramada (ControlHoras.DomainEvents)
    // es lo que el ControlDiario persiste; DetalleFranjaOrdinaria (PrivateEvents) es lo que trae el
    // evento privado entrante. Issue #288: Descripcion (dato derivado) irrelevante -> placeholder "".
    private static readonly FranjaProgramada Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");
    private static readonly DetalleFranjaOrdinaria Franja06_14Detalle =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    // Timestamps de las marcaciones que llegan antes que el turno
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    // HU-131: handler ahora requiere IPrivateEventSender para publicar DiaDepurado
    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PrivateEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento(DetalleTurno detalleTurno) =>
        new(SolicitudId, ColaboradorDetalle, Fecha, detalleTurno);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario turnoDiario) =>
        new(StreamId, Colaborador, Fecha, turnoDiario, SolicitudId);

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

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
    // CA-1 (HU-131): el handler publica DiaDepurado con CodigoColaborador, Colaborador, Fecha
    //        y ControlesDeFranja actualizados (Entrada y Salida presentes, EsAnomala=false).
    // Issue #424 (CA-3/CA-4/CA-5): el payload ademas lleva NombreTurno, la FranjaDepurada no anomala
    //        y las dos marcaciones crudas en orden cronologico.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_CalculaControlFranja_CuandoMarcacionesLlegaronAntesQueTurno()
    {
        var turnoConFranjaUnicaDetalle = new DetalleTurno("Turno Manana", [Franja06_14Detalle], "");
        Given(StreamId,
            CrearMarcacionAdicionada(Timestamp07_00),
            CrearMarcacionAdicionada(Timestamp15_00));

        await WhenAsync(CrearEvento(turnoConFranjaUnicaDetalle));

        var turnoConFranjaUnica = new TurnoDiario("Turno Manana", [Franja06_14], "");
        Then(StreamId, CrearTurnoDiarioAsignado(turnoConFranjaUnica));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, Timestamp15_00) });

        // HU-131 CA-1: publica DiaDepurado tras la depuracion. La franja 06:00-14:00 trabajada
        // 07:00-15:00 no es anomala. Issue #183 CA-4/CA-6: el payload viaja plano (HorasPorConcepto).
        // Esperado registrado A MANO con las primitivas del dominio (sin ejecutar Discriminar,
        // Consolidar ni HorasLiquidables.DesdeMinutos, la logica bajo prueba): en domingo 2026-03-15 el
        // retardo 60min (06:00-07:00) se compensa con el excedente 60min (14:00-15:00) -> retardo neto
        // 0 (no hay clave "Retardo") y queda visible la ordinaria 07:00-14:00 DominicalFestivaDiurna =
        // 420min = 7.00h. Asi un bug en esa logica no se filtra al esperado y el test lo detecta.
        // Issue #184: el DiaDepurado ahora viaja con la Trazabilidad (memoria de calculo) poblada. El
        // unico concepto del dia (ordinaria 07:00-14:00, DominicalFestivaDiurna) genera una sola linea,
        // construida con LineaConcepto desde el intervalo 07:00-14:00 y la etiqueta traducida del recurso.
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Manana",
            [new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Timestamp07_00, Timestamp15_00, false)],
            [new MarcacionDelDia(Timestamp07_00, "ENTRADA"), new MarcacionDelDia(Timestamp15_00, "ENTRADA")],
            new HorasDiscriminadas(
                new Dictionary<string, decimal> { ["DominicalFestivaDiurna"] = 7.00m },
                [LineaConcepto(new TimeOnly(7, 0), new TimeOnly(14, 0), Concepto.DominicalFestivaDiurna)])));
    }

    // CA-2 (HU-131): sin marcaciones previas, el Apply(TurnoDiarioAsignado) dispara Depurar()
    //        pero sin marcaciones el depurador retorna una franja con Entrada=null, Salida=null.
    //        DiaDepurado se emite aunque todos los ControlesDeFranja sean anomalos.
    // Issue #424 (CA-3/CA-5/CA-6): NombreTurno presente, Franjas con la franja anomala, Marcaciones
    //        vacia (sin marcaciones previas).
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PublicaDiaDepurado_CuandoNoHayMarcacionesPrevias()
    {
        // Sin Given - stream nuevo, sin marcaciones previas
        var turnoConFranjaUnicaDetalle = new DetalleTurno("Turno Manana", [Franja06_14Detalle], "");

        await WhenAsync(CrearEvento(turnoConFranjaUnicaDetalle));

        var turnoConFranjaUnica = new TurnoDiario("Turno Manana", [Franja06_14], "");
        Then(StreamId, CrearTurnoDiarioAsignado(turnoConFranjaUnica));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, null, null) }); // EsAnomala=true

        // HU-131 CA-2: DiaDepurado se publica aunque la franja sea anomala (Entrada y Salida null).
        // Issue #183 CA-6: la franja anomala no aporta horas calculables y no hay retardo, asi que
        // HorasPorConcepto viaja vacio. El contrato plano no lleva senal de anomalia (riesgo aceptado).
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Manana",
            [new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, null, null, true)],
            [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
    }

    // CA-2 (HU-131): turno sin franjas ordinarias genera ControlesDeFranja vacio.
    //        DiaDepurado se emite igual con lista vacia de controles.
    // Issue #424 (CA-6): este es el caso "dia sin jornada valida" de cero franjas -- NombreTurno
    //        presente pero Franjas vacia (biunivocidad con el descanso programado de #423): el
    //        contrato plano ya se comporta como sin depuracion (Franjas y HorasPorConcepto vacios).
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PublicaDiaDepurado_CuandoTurnoNoTieneFranjas()
    {
        // Sin Given - stream nuevo, turno sin franjas ordinarias
        var turnoSinFranjasDetalle = new DetalleTurno("Turno Sin Franjas", [], "");

        await WhenAsync(CrearEvento(turnoSinFranjasDetalle));

        var turnoSinFranjas = new TurnoDiario("Turno Sin Franjas", [], "");
        Then(StreamId, CrearTurnoDiarioAsignado(turnoSinFranjas));
        And<ControlDiarioAggregateRoot, int>(
            StreamId,
            c => c.ControlesDeFranja.Count, 0);

        // HU-131 CA-2: DiaDepurado se publica aunque el turno no tenga franjas.
        // Issue #183 CA-6: sin franjas que consolidar ni retardo, HorasPorConcepto viaja vacio.
        // Issue #424 CA-6: NombreTurno = "Turno Sin Franjas" (presente) con Franjas vacia -- la senal
        // estructural del plan (nombre + cero franjas) que #423 hara ocurrir en runtime.
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Sin Franjas",
            [],
            [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
    }
}
