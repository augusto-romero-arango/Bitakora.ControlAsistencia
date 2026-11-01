using Bitakora.ControlAsistencia.ControlHoras.AsignarAusenciaCuandoAusenciaDiariaProgramada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.CancelarAusenciaCuandoAusenciaDiariaCancelada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ResolverAmbiguedadDeMarcacionesFunction;

public static class DescarteDeResolucionPorCambioDePlanTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Dia21 = new(2026, 10, 21);
    private static readonly string StreamD = $"cd:{CodigoColaborador}:20261021";
    private static readonly ColaboradorProgramado Colaborador = new("1098765432", CodigoColaborador, "Ana Perez");
    private static readonly ResumenColaborador Resumen = new("1098765432", CodigoColaborador, "Ana Perez");
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000020");
    private static readonly Guid OtraAusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000021");

    private static DateTime D(int hora, int minuto) => Dia21.ToDateTime(new TimeOnly(hora, minuto));

    private static TurnoDiario Turno(TimeOnly inicio, TimeOnly fin) =>
        new("Turno", [new FranjaProgramada(inicio, fin, 0, [], [], string.Empty)], string.Empty, 0);

    private static DetalleTurno DetalleDe(TimeOnly inicio, TimeOnly fin) =>
        new("Turno", [new DetalleFranjaOrdinaria(inicio, fin, 0, [], [], "")], "");

    private static TurnoDiarioAsignado TurnoAsignado(TurnoDiario turno) =>
        new(StreamD, Colaborador, Dia21, turno, Guid.NewGuid());

    private static MarcacionAdicionada Marcacion(DateTime timestamp) =>
        new(StreamD, CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static ResolucionDeAmbiguedadDescartada Descartada(DiaVecino vecino) =>
        ResolucionDeAmbiguedadDescartada.Crear(StreamD, vecino);

    // Dia 06:00-14:00 con la 04:30 resuelta a favor de la vispera (cobertura de la vispera hasta 05:00).
    private static object[] DiaResuelto(params object[] extra) =>
    [
        TurnoAsignado(Turno(new TimeOnly(6, 0), new TimeOnly(14, 0))),
        Marcacion(D(4, 30)),
        Marcacion(D(6, 20)),
        Marcacion(D(14, 5)),
        CoberturaVecinaActualizada.Crear(StreamD, DiaVecino.Vispera, D(5, 0)),
        AmbiguedadDeMarcacionesResuelta.Crear(StreamD, DiaVecino.Vispera,
            [new MarcacionDecidida(D(4, 30), new DateOnly(2026, 10, 20))]),
        .. extra
    ];

    private static string PertenenciaDeLas0430(ControlDiarioAggregateRoot c) =>
        c.CrearDiaDepurado().Marcaciones.Single(m => m.Timestamp == D(4, 30)).Pertenencia!;

    public class AsignarTurnoEnLoteTests : PrivateEventHandlerAsyncTest<ProgramacionTurnoEnFechasSolicitada>
    {
        protected override IPrivateEventHandlerAsync<ProgramacionTurnoEnFechasSolicitada> Handler =>
            new ProgramacionTurnoEnFechasSolicitadaEventHandler(EventStore, PrivateEventSender);

        private static ProgramacionTurnoEnFechasSolicitada Evento(Guid solicitud, TimeOnly inicio, TimeOnly fin) =>
            new(solicitud, Resumen, [Dia21], DetalleDe(inicio, fin), null!);

        [Fact]
        public async Task ProgramacionTurnoEnFechasSolicitada_DescartaLaResolucion_CuandoElTurnoNuevoVuelveAmbigua()
        {
            Given(StreamD, DiaResuelto());
            var solicitud = Guid.NewGuid();

            await WhenAsync(Evento(solicitud, new TimeOnly(4, 0), new TimeOnly(12, 0)));

            Then(StreamD,
                new TurnoDiarioAsignado(StreamD, Colaborador, Dia21,
                    Turno(new TimeOnly(4, 0), new TimeOnly(12, 0)), solicitud),
                Descartada(DiaVecino.Vispera));
            And<ControlDiarioAggregateRoot, string>(StreamD, PertenenciaDeLas0430, "Ambigua");
        }

        [Fact]
        public async Task ProgramacionTurnoEnFechasSolicitada_DescartaLaResolucion_CuandoElTurnoNuevoDejaDeSolaparse()
        {
            Given(StreamD, DiaResuelto());
            var solicitud = Guid.NewGuid();

            await WhenAsync(Evento(solicitud, new TimeOnly(8, 0), new TimeOnly(16, 0)));

            Then(StreamD,
                new TurnoDiarioAsignado(StreamD, Colaborador, Dia21,
                    Turno(new TimeOnly(8, 0), new TimeOnly(16, 0)), solicitud),
                Descartada(DiaVecino.Vispera));
            And<ControlDiarioAggregateRoot, string>(StreamD, PertenenciaDeLas0430, "FueraDeCobertura");
        }

        [Fact]
        public async Task ProgramacionTurnoEnFechasSolicitada_ConservaLaResolucion_CuandoElTurnoEsIdenticoAlVigente()
        {
            Given(StreamD, DiaResuelto());
            var solicitud = Guid.NewGuid();

            await WhenAsync(Evento(solicitud, new TimeOnly(6, 0), new TimeOnly(14, 0)));

            Then(StreamD,
                new TurnoDiarioAsignado(StreamD, Colaborador, Dia21,
                    Turno(new TimeOnly(6, 0), new TimeOnly(14, 0)), solicitud));
            And<ControlDiarioAggregateRoot, string>(StreamD, PertenenciaDeLas0430, "FueraDeCobertura");
        }

        [Fact]
        public async Task ProgramacionTurnoEnFechasSolicitada_NoDescarta_CuandoNoHabiaResolucion()
        {
            Given(StreamD,
                TurnoAsignado(Turno(new TimeOnly(6, 0), new TimeOnly(14, 0))),
                Marcacion(D(4, 30)));
            var solicitud = Guid.NewGuid();

            await WhenAsync(Evento(solicitud, new TimeOnly(4, 0), new TimeOnly(12, 0)));

            Then(StreamD,
                new TurnoDiarioAsignado(StreamD, Colaborador, Dia21,
                    Turno(new TimeOnly(4, 0), new TimeOnly(12, 0)), solicitud));
            And<ControlDiarioAggregateRoot, string?>(StreamD, c => c.DetalleTurno!.Nombre, "Turno");
        }
    }

    public class CancelarTurnoTests : PrivateEventHandlerAsyncTest<CancelacionTurnoDiarioSolicitada>
    {
        protected override IPrivateEventHandlerAsync<CancelacionTurnoDiarioSolicitada> Handler =>
            new CancelacionTurnoDiarioSolicitadaEventHandler(EventStore, PrivateEventSender);

        // CA-4
        [Fact]
        public async Task CancelacionTurnoDiarioSolicitada_DescartaLaResolucion_CuandoCancelaElTurno()
        {
            Given(StreamD, DiaResuelto());
            var solicitud = Guid.NewGuid();

            await WhenAsync(new CancelacionTurnoDiarioSolicitada(solicitud, Resumen, Dia21));

            Then(StreamD,
                TurnoDiarioCancelado.Crear(StreamD, Colaborador, Dia21, solicitud),
                Descartada(DiaVecino.Vispera));
            And<ControlDiarioAggregateRoot, TurnoDiario?>(StreamD, c => c.DetalleTurno, null);
        }
    }

    public class AsignarAusenciaTests : PrivateEventHandlerAsyncTest<AusenciaDiariaProgramada>
    {
        protected override IPrivateEventHandlerAsync<AusenciaDiariaProgramada> Handler =>
            new AusenciaDiariaProgramadaEventHandler(EventStore, PrivateEventSender);

        // CA-4
        [Fact]
        public async Task AusenciaDiariaProgramada_DescartaLaResolucion_CuandoLaAusenciaEsNueva()
        {
            Given(StreamD, DiaResuelto());

            await WhenAsync(new AusenciaDiariaProgramada(AusenciaId, Resumen, Dia21, "Vacaciones"));

            Then(StreamD,
                AusenciaDiariaAsignada.Crear(StreamD, Colaborador, Dia21, AusenciaId, "Vacaciones"),
                Descartada(DiaVecino.Vispera));
            And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 3);
        }

        [Fact]
        public async Task AusenciaDiariaProgramada_ConservaLaResolucion_CuandoLaAusenciaYaEstabaVigente()
        {
            Given(StreamD, DiaResuelto(
                AusenciaDiariaAsignada.Crear(StreamD, Colaborador, Dia21, AusenciaId, "Vacaciones")));

            await WhenAsync(new AusenciaDiariaProgramada(AusenciaId, Resumen, Dia21, "Vacaciones"));

            Then(StreamD);
            And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 3);
        }

        [Fact]
        public async Task AusenciaDiariaProgramada_ConservaLaResolucion_CuandoLaAusenciaFueCanceladaAntes()
        {
            Given(StreamD, DiaResuelto(
                CancelacionAusenciaDiariaRegistrada.Crear(StreamD, OtraAusenciaId, Dia21)));

            await WhenAsync(new AusenciaDiariaProgramada(OtraAusenciaId, Resumen, Dia21, "Vacaciones"));

            Then(StreamD);
            And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 3);
        }
    }

    public class CancelarAusenciaTests : PrivateEventHandlerAsyncTest<AusenciaDiariaCancelada>
    {
        protected override IPrivateEventHandlerAsync<AusenciaDiariaCancelada> Handler =>
            new AusenciaDiariaCanceladaEventHandler(EventStore, PrivateEventSender);

        // CA-4
        [Fact]
        public async Task AusenciaDiariaCancelada_DescartaLaResolucion_CuandoLiberaLaAusenciaVigente()
        {
            Given(StreamD, DiaResuelto(
                AusenciaDiariaAsignada.Crear(StreamD, Colaborador, Dia21, AusenciaId, "Vacaciones")));

            await WhenAsync(new AusenciaDiariaCancelada(AusenciaId, Resumen, Dia21));

            Then(StreamD,
                CancelacionAusenciaDiariaRegistrada.Crear(StreamD, AusenciaId, Dia21),
                Descartada(DiaVecino.Vispera));
            And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 3);
        }

        [Fact]
        public async Task AusenciaDiariaCancelada_ConservaLaResolucion_CuandoLaCancelacionSoloSeRecuerda()
        {
            Given(StreamD, DiaResuelto(
                AusenciaDiariaAsignada.Crear(StreamD, Colaborador, Dia21, AusenciaId, "Vacaciones")));

            await WhenAsync(new AusenciaDiariaCancelada(OtraAusenciaId, Resumen, Dia21));

            Then(StreamD, CancelacionAusenciaDiariaRegistrada.Crear(StreamD, OtraAusenciaId, Dia21));
            And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 3);
        }
    }
}
