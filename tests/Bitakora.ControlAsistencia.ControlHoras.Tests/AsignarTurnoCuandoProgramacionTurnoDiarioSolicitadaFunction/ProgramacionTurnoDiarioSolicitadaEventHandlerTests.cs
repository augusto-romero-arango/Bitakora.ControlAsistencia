// HU-12 / issue #210: Asignar turno diario al control cuando llega ProgramacionTurnoDiarioSolicitada.
// ADR-0024 #8: el evento privado intra-BC se consume directo con IPrivateEventHandlerAsync, sin comando espejo.

using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class ProgramacionTurnoDiarioSolicitadaEventHandlerTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    // Datos de prueba fijos - el stream ID es determinista a partir de CodigoColaborador+Fecha
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    // Issue #322: Colaborador (ControlHoras.DomainEvents) -- el tipo que persiste TurnoDiarioAsignado.
    private static readonly ColaboradorProgramado Colaborador = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    // Mismo colaborador, en la forma con que llega dentro del evento privado; el handler lo mapea
    // a Colaborador para TurnoDiarioAsignado (CA-ADR-0029 decision #5).
    private static readonly DetalleColaborador ColaboradorDetalle = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // CA-7: stream ID determinista que el handler debe computar internamente
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    // El evento privado sigue trayendo DetalleTurno (PrivateEvents) sin cambios.
    // Issue #288: Descripcion (dato derivado) es irrelevante para este test -> placeholder "".
    private static readonly DetalleTurno DetalleTurnoTest = new(
        "Turno Manana",
        [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "");

    // Issue #322: TurnoDiario (ControlHoras.DomainEvents) -- lo que el handler persiste, mapeado
    // desde DetalleTurnoTest (mismos valores, tipo nuevo).
    private static readonly TurnoDiario TurnoDiarioTest = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "");

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PrivateEventSender);

    private static ProgramacionTurnoDiarioSolicitada CrearEvento() =>
        new(SolicitudId, ColaboradorDetalle, Fecha, DetalleTurnoTest);

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado() =>
        new(StreamId, Colaborador, Fecha, TurnoDiarioTest, SolicitudId);

    // CA-3: NO existe ControlDiario para CodigoColaborador+Fecha - el handler inicia el stream
    // CA-5: el evento incluye InformacionColaborador, Fecha, DetalleTurno y SolicitudId
    // CA-6: el aggregate actualiza InformacionColaborador, Fecha y DetalleTurno
    // CA-7: el stream ID resultante es "cd:{CodigoColaborador}:{Fecha:yyyyMMdd}" (issue #420)
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_EmiteTurnoDiarioAsignado_CuandoNoExisteControlDiario()
    {
        // Sin Given - el stream no existe para este CodigoColaborador+Fecha
        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioAsignado());
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        And<ControlDiarioAggregateRoot, ColaboradorProgramado?>(StreamId, c => c.InformacionColaborador, Colaborador);
        And<ControlDiarioAggregateRoot, DateOnly>(StreamId, c => c.Fecha, Fecha);
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, TurnoDiarioTest.Nombre);
    }

    // CA-4: YA existe ControlDiario para CodigoColaborador+Fecha - el handler agrega al stream existente
    // CA-5: el nuevo evento contiene todos los campos actualizados
    // CA-8: el segundo mensaje opera sobre el mismo stream (mismo CodigoColaborador+Fecha = mismo StreamId)
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_EmiteTurnoDiarioAsignado_CuandoYaExisteControlDiario()
    {
        var solicitudAnteriorId = Guid.Parse("019600b0-0000-7000-8000-000000000002");
        var turnoAnterior = new TurnoDiarioAsignado(StreamId, Colaborador, Fecha, TurnoDiarioTest, solicitudAnteriorId);

        // Pre-carga el stream con el mismo StreamId que usara el handler (CA-8)
        Given(StreamId, turnoAnterior);
        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioAsignado());
        And<ControlDiarioAggregateRoot, Guid>(StreamId, c => c.UltimaSolicitudId, SolicitudId);
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, TurnoDiarioTest.Nombre);
    }

    // CA-4 (agregado en revision): el mapeo del Function App es recursivo hasta las sub-franjas,
    // pero los dos tests anteriores solo pasan franjas con Descansos/Extras vacios, asi que
    // MapearFranja/MapearSubFranja quedaban sin ejercitar. Aqui el evento privado entrante trae un
    // descanso y una extra con offsets distintos y Descripcion no vacia en los tres niveles: si el
    // mapeo perdiera un campo o cruzara DiaOffsetInicio/DiaOffsetFin, la comparacion
    // member-by-member de Then/And lo delata.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_MapeaDescansosYExtrasConSusOffsets_CuandoLaFranjaTraeSubFranjas()
    {
        var turnoEntrante = new DetalleTurno(
            "Turno Nocturno",
            [
                new DetalleFranjaOrdinaria(
                    new TimeOnly(22, 0), new TimeOnly(6, 0), 1,
                    [new DetalleSubFranja(new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)")],
                    [new DetalleSubFranja(new TimeOnly(6, 0), new TimeOnly(8, 0), 1, 1, "(06:00+1-08:00+1)")],
                    "(22:00-06:00+1)")
            ],
            "Turno Nocturno (22:00-06:00+1)");

        await WhenAsync(new ProgramacionTurnoDiarioSolicitada(
            SolicitudId, ColaboradorDetalle, Fecha, turnoEntrante));

        // Oraculo construido a mano, no derivado del mapeo bajo prueba (MEF-ADR-0002).
        var turnoPersistidoEsperado = new TurnoDiario(
            "Turno Nocturno",
            [
                new FranjaProgramada(
                    new TimeOnly(22, 0), new TimeOnly(6, 0), 1,
                    [new SubFranjaProgramada(new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)")],
                    [new SubFranjaProgramada(new TimeOnly(6, 0), new TimeOnly(8, 0), 1, 1, "(06:00+1-08:00+1)")],
                    "(22:00-06:00+1)")
            ],
            "Turno Nocturno (22:00-06:00+1)");

        Then(StreamId, new TurnoDiarioAsignado(
            StreamId, Colaborador, Fecha, turnoPersistidoEsperado, SolicitudId));
        And<ControlDiarioAggregateRoot, TurnoDiario?>(
            StreamId, c => c.DetalleTurno, turnoPersistidoEsperado);
    }

    // Issue #336 CA-1: el evento de bus trae la sede EFECTIVA ya resuelta por la cascada del lado
    // de Programacion (#341) en cada franja -- el handler la propaga (DetalleSede -> SedeProgramada,
    // mapeo mecanico) al persistir TurnoDiarioAsignado. La segunda franja llega sin sede y el mapeo
    // tolerante la deja null: no todas las franjas de un turno multi-sede tienen que traerla.
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_PropagaLaSedeDeCadaFranja_CuandoElEventoTraeSedesEfectivas()
    {
        var sedeSubaDetalle = new DetalleSede("SEDE-SUBA", "Suba");
        var turnoEntrante = new DetalleTurno(
            "Turno Partido",
            [
                new DetalleFranjaOrdinaria(
                    new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], "(06:00-10:00)", sedeSubaDetalle),
                new DetalleFranjaOrdinaria(
                    new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "(14:00-18:00)")
            ],
            "Turno Partido");

        await WhenAsync(new ProgramacionTurnoDiarioSolicitada(
            SolicitudId, ColaboradorDetalle, Fecha, turnoEntrante));

        // Oraculo construido a mano, no derivado del mapeo bajo prueba (MEF-ADR-0002).
        var sedeSubaEsperada = new SedeProgramada("SEDE-SUBA", "Suba");
        var turnoPersistidoEsperado = new TurnoDiario(
            "Turno Partido",
            [
                new FranjaProgramada(
                    new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], "(06:00-10:00)", sedeSubaEsperada),
                new FranjaProgramada(
                    new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "(14:00-18:00)")
            ],
            "Turno Partido");

        Then(StreamId, new TurnoDiarioAsignado(
            StreamId, Colaborador, Fecha, turnoPersistidoEsperado, SolicitudId));
        And<ControlDiarioAggregateRoot, SedeProgramada?>(
            StreamId, c => c.DetalleTurno!.FranjasOrdinarias[0].Sede, sedeSubaEsperada);
        And<ControlDiarioAggregateRoot, SedeProgramada?>(
            StreamId, c => c.DetalleTurno!.FranjasOrdinarias[1].Sede, null);
    }

    // Issue #336 CA-2: evento de bus sin sedes en las franjas -> comportamiento actual intacto,
    // todas las franjas persisten con Sede null (regresion explicita, en verde).
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_DejaLaSedeEnNull_CuandoElEventoNoTraeSedesEnLasFranjas()
    {
        await WhenAsync(CrearEvento());

        Then(StreamId, CrearTurnoDiarioAsignado());
        And<ControlDiarioAggregateRoot, SedeProgramada?>(
            StreamId, c => c.DetalleTurno!.FranjasOrdinarias[0].Sede, null);
    }
}
