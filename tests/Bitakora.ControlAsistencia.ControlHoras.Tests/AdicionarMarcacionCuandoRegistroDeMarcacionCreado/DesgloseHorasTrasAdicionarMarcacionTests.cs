// HU-139: Integrar consolidador DesgloseHoras al flujo reactivo del ControlDiario
// Issue #270: el evento privado que dispara el flujo cambia de MarcacionRegistrada a
// RegistroDeMarcacionCreado (CA-3, CA-5); el comportamiento verificado aqui no cambia.
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

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

public class DesgloseHorasTrasAdicionarMarcacionTests : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    // Datos de prueba fijos - mismo ancla de fecha y stream ID compuesto que los tests del handler
    private const string EmpleadoId = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"{EmpleadoId}:{Fecha:yyyy-MM-dd}";

    // Issue #322: Empleado (ControlHoras.DomainEvents) -- el tipo que persiste TurnoDiarioAsignado.
    private static readonly ColaboradorProgramado Empleado = new(
        EmpleadoId, "CC", "1234567890", "Luis Augusto", "Barreto");
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000003");

    // CA-1: turno partido con dos franjas ordinarias 08:00-12:00 y 14:00-18:00
    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
    private static readonly FranjaProgramada Franja08_12 =
        new(new TimeOnly(8, 0), new TimeOnly(12, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");

    // Timestamps de marcaciones (fuera de ventana nocturna: >= 04:00)
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp08_00 = new(2026, 3, 15, 8, 0, 0);
    private static readonly DateTime Timestamp12_05 = new(2026, 3, 15, 12, 5, 0);
    private static readonly DateTime Timestamp14_10 = new(2026, 3, 15, 14, 10, 0);
    private static readonly DateTime Timestamp18_30 = new(2026, 3, 15, 18, 30, 0);

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PublicEventSender);

    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(DateTime timestamp) =>
        new(EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, EmpleadoId, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario detalleTurno) =>
        new(StreamId, Empleado, Fecha, detalleTurno, SolicitudId);

    // CA-1: con TurnoDiarioAsignado (turno partido 08:00-12:00 y 14:00-18:00) previo y
    //       MarcacionAdicionada previas (08:00, 12:05, 14:10), el RegistroDeMarcacionCreado a las 18:30
    //       completa la ultima franja y dispara el recalculo. DesgloseHoras refleja la consolidacion
    //       del dia con las dos franjas no anomalas (extras ajustadas por compensacion si aplica).
    [Fact]
    public async Task RegistroDeMarcacionCreado_RecalculaDesgloseHoras_CuandoMarcacionCompletaUltimaFranja()
    {
        var turnoPartido = new TurnoDiario("Turno Partido", [Franja08_12, Franja14_18], "");
        Given(StreamId,
            CrearTurnoDiarioAsignado(turnoPartido),
            CrearMarcacionAdicionada(Timestamp08_00),
            CrearMarcacionAdicionada(Timestamp12_05),
            CrearMarcacionAdicionada(Timestamp14_10));

        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp18_30));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp18_30));

        // Depuracion esperada: F1(08:00, 12:05) y F2(14:10, 18:30), ambas con entrada y salida.
        // Anclar los ControlesDeFranja documenta los insumos exactos del oraculo de DesgloseHoras.
        var controlFranja1 = new ControlFranja(Franja08_12, Timestamp08_00, Timestamp12_05);
        var controlFranja2 = new ControlFranja(Franja14_18, Timestamp14_10, Timestamp18_30);
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { controlFranja1, controlFranja2 });

        // Esperado registrado A MANO con las primitivas del dominio (sin ejecutar Consolidar ni
        // CalcularDesglose, la logica bajo prueba), para que un bug en esa logica no se filtre al
        // esperado y el test si detecte regresiones. Escenario en domingo 2026-03-15:
        //   F1 08:00-12:00 trabajada 08:00-12:05: ordinaria 08:00-12:00 DominicalFestivaDiurna +
        //     excedente 12:00-12:05 ExtraDiurnaDominicalFestiva (5min, sin retardo). Retardo vacio.
        //   F2 14:00-18:00 trabajada 14:10-18:30: retardo 10min (14:00-14:10) compensado por 10min del
        //     excedente; ordinaria 14:10-18:00 DominicalFestivaDiurna + excedente visible 18:00-18:20
        //     ExtraDiurnaDominicalFestiva (20min de 30min; los ultimos 10min compensan el retardo).
        // RetardoTotal del dia = el de F2 (F1 no aporta retardo; el retardo neto del dia es 0, asi que
        // no hay compensacion cross-franja). FranjasAnomalas = 0.
        var f1Ordinaria = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(8, 0)),
                new MomentoDelDia(new TimeOnly(12, 0))),
            Concepto.DominicalFestivaDiurna);
        var f1Excedente = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(12, 0)),
                new MomentoDelDia(new TimeOnly(12, 5))),
            Concepto.ExtraDiurnaDominicalFestiva);
        var franja1 = new DesgloseFranja(Franja08_12, [f1Ordinaria, f1Excedente], Retardo.Vacio);

        var f2Ordinaria = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(14, 10)),
                new MomentoDelDia(new TimeOnly(18, 0))),
            Concepto.DominicalFestivaDiurna);
        var f2Excedente = new IntervaloClasificado(
            IntervaloTemporal.Crear(
                new MomentoDelDia(new TimeOnly(18, 0)),
                new MomentoDelDia(new TimeOnly(18, 20))),
            Concepto.ExtraDiurnaDominicalFestiva);
        var retardoF2 = Retardo.Crear(
            [IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(14, 0)), new MomentoDelDia(new TimeOnly(14, 10)))],
            [IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(18, 20)), new MomentoDelDia(new TimeOnly(18, 30)))]);
        var franja2 = new DesgloseFranja(Franja14_18, [f2Ordinaria, f2Excedente], retardoF2);

        var esperado = new DesgloseHoras([franja1, franja2], retardoF2, FranjasAnomalas: 0);

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            esperado);
    }

    // CA-2: sin turno previo (ningun Given), el RegistroDeMarcacionCreado crea el aggregate sin DetalleTurno.
    //       Depurar() retorna lista vacia -> RecalcularDesgloseHoras no tiene franjas que consolidar
    //       y DesgloseHoras queda en DesgloseHoras.Vacio.
    [Fact]
    public async Task RegistroDeMarcacionCreado_DejaDesgloseHorasVacio_CuandoNoHayTurnoPrevio()
    {
        // Sin Given - el aggregate nace solo con la marcacion (DetalleTurno queda null)
        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));

        And<ControlDiarioAggregateRoot, DesgloseHoras>(
            StreamId,
            c => c.DesgloseHoras,
            DesgloseHoras.Vacio);
    }
}
