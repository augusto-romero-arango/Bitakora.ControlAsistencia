using AwesomeAssertions;
using Cosmos.EventDriven.Abstractions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class RepartirDescarteDeResolucionTests : CommandHandlerAsyncTest<RepartirMarcaciones>
{
    private const string CodigoColaborador = "EMP-001";
    private const string Dispositivo = "DEV-001";

    private static readonly DateOnly Dia20 = new(2026, 10, 20);
    private static readonly DateOnly Dia21 = new(2026, 10, 21);
    private static readonly DateOnly Dia22 = new(2026, 10, 22);

    private static readonly string StreamV = $"cd:{CodigoColaborador}:20261020";
    private static readonly string StreamD = $"cd:{CodigoColaborador}:20261021";
    private static readonly string StreamN = $"cd:{CodigoColaborador}:20261022";

    protected override ICommandHandlerAsync<RepartirMarcaciones> Handler =>
        new RepartirMarcacionesCommandHandler(EventStore, PrivateEventSender);

    private static DateTime V(int hora, int minuto) => Dia20.ToDateTime(new TimeOnly(hora, minuto));
    private static DateTime D(int hora, int minuto) => Dia21.ToDateTime(new TimeOnly(hora, minuto));

    private static TurnoDiario Turno(TimeOnly inicio, TimeOnly fin, int offsetFin) =>
        new("Turno", [new FranjaProgramada(inicio, fin, offsetFin, [], [], string.Empty)], string.Empty, 0);

    private static TurnoDiarioAsignado TurnoAsignado(string streamId, DateOnly fecha, TurnoDiario turno) =>
        new(streamId, new ColaboradorProgramado("1098765432", CodigoColaborador, "Ana Perez"),
            fecha, turno, Guid.NewGuid());

    private static MarcacionAdicionada Marcacion(string streamId, DateTime timestamp) =>
        new(streamId, CodigoColaborador, timestamp, "ENTRADA", Dispositivo);

    private static MarcacionDecidida[] LaDeLas0430A(DateOnly fecha) => [new(D(4, 30), fecha)];

    private static string PertenenciaDeLas0430(ControlDiarioAggregateRoot c) =>
        c.CrearDiaDepurado().Marcaciones.Single(m => m.Timestamp == D(4, 30)).Pertenencia!;

    // Vispera 17:00-01:00 con su mitad de la resolucion vigente; el dia (el que cambio de plan) ya
    // la descarto y ahora tiene el turno nuevo.
    private void GivenVisperaConResolucionYDiaQueYaDescarto(TurnoDiario turnoDelDia, DateTime limiteInicioDia)
    {
        Given(StreamV,
            TurnoAsignado(StreamV, Dia20, Turno(new TimeOnly(17, 0), new TimeOnly(1, 0), 1)),
            Marcacion(StreamV, V(16, 55)),
            Marcacion(StreamV, D(4, 30)),
            CoberturaVecinaActualizada.Crear(StreamV, DiaVecino.DiaSiguiente, limiteInicioDia),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamV, DiaVecino.DiaSiguiente, LaDeLas0430A(Dia20)));
        Given(StreamD,
            TurnoAsignado(StreamD, Dia21, turnoDelDia),
            Marcacion(StreamD, D(4, 30)),
            Marcacion(StreamD, D(6, 20)),
            Marcacion(StreamD, D(14, 5)),
            CoberturaVecinaActualizada.Crear(StreamD, DiaVecino.Vispera, D(5, 0)),
            CierreDeLaVisperaActualizado.Crear(StreamD, D(1, 0)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamD, DiaVecino.Vispera, LaDeLas0430A(Dia20)),
            ResolucionDeAmbiguedadDescartada.Crear(StreamD, DiaVecino.Vispera));
    }

    // CA-1: el turno del dia paso a 04:00-12:00; la vispera descarta su mitad y la 04:30 vuelve a ser ambigua.
    [Fact]
    public async Task RepartirMarcaciones_DescartaLaResolucionDeLaVispera_CuandoElDiaCambioDePlanYSigueAmbigua()
    {
        GivenVisperaConResolucionYDiaQueYaDescarto(
            Turno(new TimeOnly(4, 0), new TimeOnly(12, 0), 0), D(2, 0));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(StreamV, ResolucionDeAmbiguedadDescartada.Crear(StreamV, DiaVecino.DiaSiguiente));
        Then(StreamD);
        And<ControlDiarioAggregateRoot, string>(StreamV, PertenenciaDeLas0430, "Ambigua");
    }

    // CA-2: el turno del dia paso a 08:00-16:00; sin solape, la 04:30 es salida de la vispera.
    [Fact]
    public async Task RepartirMarcaciones_DescartaLaResolucionDeLaVispera_CuandoElDiaCambioDePlanYYaNoHaySolape()
    {
        GivenVisperaConResolucionYDiaQueYaDescarto(
            Turno(new TimeOnly(8, 0), new TimeOnly(16, 0), 0), D(6, 0));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(StreamV, ResolucionDeAmbiguedadDescartada.Crear(StreamV, DiaVecino.DiaSiguiente));
        Then(StreamD);
        And<ControlDiarioAggregateRoot, string>(StreamV, PertenenciaDeLas0430, "DelTurno");
    }

    // CA-5: el dia tiene resolucion con la vispera y con el dia siguiente; la vispera ya la descarto,
    // asi que solo se descarta el par con la vispera.
    [Fact]
    public async Task RepartirMarcaciones_DescartaSoloElParConLaVispera_CuandoElDiaTambienResolvioConElSiguiente()
    {
        Given(StreamV,
            Marcacion(StreamV, V(16, 55)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamV, DiaVecino.DiaSiguiente, LaDeLas0430A(Dia20)),
            ResolucionDeAmbiguedadDescartada.Crear(StreamV, DiaVecino.DiaSiguiente));
        Given(StreamD,
            TurnoAsignado(StreamD, Dia21, Turno(new TimeOnly(6, 0), new TimeOnly(14, 0), 0)),
            Marcacion(StreamD, D(4, 30)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamD, DiaVecino.Vispera, LaDeLas0430A(Dia20)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamD, DiaVecino.DiaSiguiente, LaDeLas0430A(Dia22)));
        Given(StreamN,
            Marcacion(StreamN, new DateTime(2026, 10, 22, 8, 0, 0)),
            CierreDeLaVisperaActualizado.Crear(StreamN, D(14, 0)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamN, DiaVecino.Vispera, LaDeLas0430A(Dia22)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(StreamD, ResolucionDeAmbiguedadDescartada.Crear(StreamD, DiaVecino.Vispera));
        Then(StreamV);
        Then(StreamN);
        And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 1);
    }

    [Fact]
    public async Task RepartirMarcaciones_NoDescarta_CuandoAmbosDiasConservanLaResolucion()
    {
        Given(StreamV,
            Marcacion(StreamV, V(16, 55)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamV, DiaVecino.DiaSiguiente, LaDeLas0430A(Dia20)));
        Given(StreamD,
            TurnoAsignado(StreamD, Dia21, Turno(new TimeOnly(6, 0), new TimeOnly(14, 0), 0)),
            Marcacion(StreamD, D(4, 30)),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamD, DiaVecino.Vispera, LaDeLas0430A(Dia20)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(StreamV);
        Then(StreamD);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, int>(StreamD, c => c.Marcaciones.Count, 1);
    }
}
