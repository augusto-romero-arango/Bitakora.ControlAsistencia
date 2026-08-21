// HU-123: Integrar depurador al ControlDiario de forma reactiva
// HU-108: Emitir DiaDepurado tras adicionar marcacion (CA-1, CA-2, CA-3, CA-4, CA-5)
// Issue #270: el evento privado que dispara el flujo cambia de MarcacionRegistrada a
// RegistroDeMarcacionCreado (CA-3, CA-5); el comportamiento verificado aqui no cambia.
// Familia 1: verifica que Apply(MarcacionAdicionada) dispara el recalculo de ControlesDeFranja
// y que el handler publica DiaDepurado via IPrivateEventSender tras cada recalculo.

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

public class DepurarAlAdicionarMarcacionTests : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    // Datos de prueba fijos - misma ancla de fecha que los tests del handler
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{CodigoColaborador}:{Fecha:yyyyMMdd}";
    private static readonly string StreamIdDiaAnterior = $"cd:{CodigoColaborador}:20260314";

    // Issue #322: Colaborador (ControlHoras.DomainEvents) para construir TurnoDiarioAsignado en Given.
    private static readonly ColaboradorProgramado ColaboradorPersistido = new(
        CodigoColaborador, "CC", "1234567890", "Luis Augusto", "Barreto");

    // Oraculo a mano de la composicion que hace CrearResumenColaborador(): Identificacion
    // "{Tipo}-{Numero}" y NombreCompleto "{Nombres} {Apellidos}" de ColaboradorPersistido.
    private static readonly ResumenColaborador ColaboradorResumen = new(
        "CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000001");

    // CA-1: franja unica 06:00-14:00
    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
    private static readonly FranjaProgramada Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    // CA-3: turno partido con dos franjas
    private static readonly FranjaProgramada Franja06_12 =
        new(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");

    // Timestamps de marcaciones (fuera de ventana nocturna: >= 04:00)
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp05_50 = new(2026, 3, 15, 5, 50, 0);
    private static readonly DateTime Timestamp12_05 = new(2026, 3, 15, 12, 5, 0);
    private static readonly DateTime Timestamp14_10 = new(2026, 3, 15, 14, 10, 0);

    // HU-108: handler ahora requiere IPrivateEventSender para publicar DiaDepurado
    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PrivateEventSender);

    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(DateTime timestamp) =>
        new(CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario detalleTurno) =>
        new(StreamId, ColaboradorPersistido, Fecha, detalleTurno, SolicitudId);

    // Issue #184: oraculo independiente de una linea de trazabilidad (memoria de calculo). Se arma a
    // mano desde IntervaloTemporal.ToString() (primitiva ya probada) y la etiqueta traducida del recurso
    // (no un literal), sin ejecutar Discriminar (regla 20). Mismo patron que DesgloseHorasDiscriminarTests.
    private static string LineaConcepto(TimeOnly inicio, TimeOnly fin, Concepto concepto) =>
        $"{IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin))}: " +
        $"{IntervaloClasificado.Mensajes.Etiqueta(concepto)}";

    // CA-1 (HU-123): con TurnoDiarioAsignado (franja unica 06:00-14:00) previo y RegistroDeMarcacionCreado a las 07:00,
    //        ControlesDeFranja debe quedar con un ControlFranja(Franja06_14, Entrada=07:00, Salida=null).
    //        Verifica que el hook reactivo se dispara desde Apply(MarcacionAdicionada).
    // CA-1 (HU-108): el handler publica DiaDepurado con CodigoColaborador, Colaborador, Fecha y las
    //        horas discriminadas correctas.
    [Fact]
    public async Task RegistroDeMarcacionCreado_CalculaControlFranja_CuandoHayTurnoYLlegaMarcacion()
    {
        var turnoUnico = new TurnoDiario("Turno Manana", [Franja06_14], "");
        Given(StreamId, CrearTurnoDiarioAsignado(turnoUnico));

        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, null) });

        // HU-108 CA-1: el handler publica un DiaDepurado con el estado tras el recalculo.
        // Issue #183 CA-4/CA-6: el payload viaja plano (HorasDiscriminadas), sin ControlesDeFranja.
        // La franja quedo anomala (Salida null): no aporta minutos calculables y no hay retardo, asi
        // que MinutosPorConcepto viaja vacio. Nota del issue (riesgo aceptado): el contrato plano ya no
        // distingue "franja anomala" de "dia sin horas"; ambos publican el mismo diccionario vacio.
        ThenIsPublishedPrivately(new DiaDepurado(
            CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            new HorasDiscriminadas(new Dictionary<string, int>(), [])));
    }

    // CA-2 (HU-123): sin turno previo, la marcacion crea el aggregate sin TurnoDiario.
    //        Depurar() retorna lista vacia porque DepuradorDeMarcaciones.Depurar(null,...) -> [].
    //        Verifica el caso "marcacion llega antes del turno" donde el aggregate
    //        se inicia solo con marcacion y no hay nada que depurar.
    // CA-2/CA-4 (HU-108): el handler publica DiaDepurado incluso cuando ControlesDeFranja esta vacio,
    //        con Colaborador null pero CodigoColaborador top-level presente.
    [Fact]
    public async Task RegistroDeMarcacionCreado_DejaControlesDeFranjaVacios_CuandoNoHayTurnoPrevio()
    {
        // Sin Given - el aggregate se crea solo con la marcacion (TurnoDiario queda null)
        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);

        // HU-108 CA-4: se publica DiaDepurado aunque no haya turno. Colaborador es null porque el
        // ControlDiario nacio solo por marcacion; CodigoColaborador top-level sigue presente.
        // Issue #183 CA-6: sin turno no hay nada que consolidar -> MinutosPorConcepto viaja vacio.
        ThenIsPublishedPrivately(new DiaDepurado(
            CodigoColaborador,
            Fecha,
            null,
            new HorasDiscriminadas(new Dictionary<string, int>(), [])));
    }

    // CA-3 (HU-123): con turno partido (06:00-12:00 y 14:00-18:00) y 2 MarcacionAdicionada previas
    //        (05:50 y 12:05), el nuevo RegistroDeMarcacionCreado a las 14:10 dispara el recalculo
    //        completo: F1(Entrada=05:50, Salida=12:05) + F2(Entrada=14:10, Salida=null).
    //        Verifica comportamiento idem-potente: no acumula sobre resultado anterior.
    // CA-3 (HU-108): el handler publica DiaDepurado; Issue #183: con el payload plano (HorasDiscriminadas).
    [Fact]
    public async Task RegistroDeMarcacionCreado_RecalculaControlesDeFranjaCompletos_CuandoHayTurnoPartidoYMarcacionesAcumuladas()
    {
        var turnoPartido = new TurnoDiario("Turno Partido", [Franja06_12, Franja14_18], "");
        Given(StreamId,
            CrearTurnoDiarioAsignado(turnoPartido),
            CrearMarcacionAdicionada(Timestamp05_50),
            CrearMarcacionAdicionada(Timestamp12_05));

        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp14_10));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp14_10));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[]
            {
                new(Franja06_12, Timestamp05_50, Timestamp12_05),
                new(Franja14_18, Timestamp14_10, null)
            });

        // HU-108 CA-3: las dos franjas se recalculan. F1: no anomala (Entrada y Salida). F2: anomala
        // (Salida null). Issue #183 CA-4/CA-6: el payload viaja plano (MinutosPorConcepto). Solo F1
        // (no anomala) aporta minutos; F2 anomala no aporta. El contrato plano no lleva senal de la
        // franja anomala (riesgo aceptado del issue).
        // Esperado registrado A MANO con las primitivas del dominio (sin ejecutar Discriminar ni
        // Consolidar, la logica bajo prueba). F1 06:00-12:00 trabajada 05:50-12:05 en domingo
        // 2026-03-15: entro 05:50 -> recortado a 06:00 (sin retardo); ordinaria 06:00-12:00
        // DominicalFestivaDiurna = 360min y excedente 12:00-12:05 ExtraDiurnaDominicalFestiva = 5min
        // (sin retardo que lo compense). Sin retardo neto -> no hay clave "Retardo".
        // Issue #184: el DiaDepurado ahora viaja con la Trazabilidad (memoria de calculo) poblada. Dos
        // conceptos -> dos lineas, en orden cronologico (ordinaria antes que excedente), cada una armada
        // con LineaConcepto desde su intervalo y la etiqueta traducida. F2 es anomala (Salida null): no
        // aporta intervalos ni lineas.
        ThenIsPublishedPrivately(new DiaDepurado(
            CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            new HorasDiscriminadas(
                new Dictionary<string, int>
                {
                    ["DominicalFestivaDiurna"] = 360,
                    ["ExtraDiurnaDominicalFestiva"] = 5
                },
                [
                    LineaConcepto(new TimeOnly(6, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna),
                    LineaConcepto(new TimeOnly(12, 0), new TimeOnly(12, 5), Concepto.ExtraDiurnaDominicalFestiva)
                ])));
    }

    // CA-5 (HU-108): marcacion a las 02:00 cae en ventana nocturna [00:00, 04:00).
    // El handler procesa dos fechas-destino: dia calendario (2026-03-15) y dia anterior (2026-03-14).
    // Publica dos DiaDepurado - uno por cada fecha, en el orden de procesamiento del handler.
    // Verifica con ThenIsPublishedPrivately(evento1, evento2): count exacto + orden exacto.
    [Fact]
    public async Task RegistroDeMarcacionCreado_PublicaDosDiaDepurado_CuandoMarcacionEstaEnVentanaNocturna()
    {
        // Sin Given - ninguno de los dos streams existe
        var timestampNocturno = new DateTime(2026, 3, 15, 2, 0, 0);
        var fechaDiaCal = Fecha;                        // 2026-03-15 (dia calendario)
        var fechaDiaAnt = Fecha.AddDays(-1);            // 2026-03-14 (dia anterior)

        await WhenAsync(CrearRegistroDeMarcacionCreado(timestampNocturno));

        // Verificar que cada stream tiene exactamente un MarcacionAdicionada
        Then(StreamId,
            new MarcacionAdicionada(StreamId, CodigoColaborador, timestampNocturno, "ENTRADA", "DEV-001"));
        Then(StreamIdDiaAnterior,
            new MarcacionAdicionada(StreamIdDiaAnterior, CodigoColaborador, timestampNocturno, "ENTRADA", "DEV-001"));

        // CA-5: dos DiaDepurado publicados, en orden: dia calendario primero, dia anterior segundo.
        // Ambos sin turno previo (Colaborador=null), pero con CodigoColaborador top-level.
        // Issue #183 CA-6: sin turno en ninguno de los dos streams, MinutosPorConcepto viaja vacio en ambos.
        ThenIsPublishedPrivately(
            new DiaDepurado(CodigoColaborador, fechaDiaCal, null, new HorasDiscriminadas(new Dictionary<string, int>(), [])),
            new DiaDepurado(CodigoColaborador, fechaDiaAnt, null, new HorasDiscriminadas(new Dictionary<string, int>(), [])));

        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);
        And<ControlDiarioAggregateRoot, int>(
            StreamIdDiaAnterior, c => c.ControlesDeFranja.Count, 0);
    }
}
