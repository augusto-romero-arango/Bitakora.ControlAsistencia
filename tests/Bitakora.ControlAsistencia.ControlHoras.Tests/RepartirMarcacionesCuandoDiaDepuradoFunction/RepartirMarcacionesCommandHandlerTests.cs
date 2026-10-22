using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class RepartirMarcacionesCommandHandlerTests : CommandHandlerAsyncTest<RepartirMarcaciones>
{
    private const string CodigoColaborador = "EMP-001";
    private const string Dispositivo = "DEV-001";

    private static readonly DateOnly Dia20 = new(2026, 4, 20);
    private static readonly DateOnly Dia21 = new(2026, 4, 21);
    private static readonly DateOnly Dia22 = new(2026, 4, 22);

    private static readonly string Stream19 = $"cd:{CodigoColaborador}:20260419";
    private static readonly string Stream20 = $"cd:{CodigoColaborador}:20260420";
    private static readonly string Stream21 = $"cd:{CodigoColaborador}:20260421";
    private static readonly string Stream22 = $"cd:{CodigoColaborador}:20260422";

    protected override ICommandHandlerAsync<RepartirMarcaciones> Handler =>
        new RepartirMarcacionesCommandHandler(EventStore, PrivateEventSender);

    private static FranjaProgramada Franja(TimeOnly inicio, TimeOnly fin, int offsetFin) =>
        new(inicio, fin, offsetFin, [], [], string.Empty);

    private static TurnoDiario Turno(FranjaProgramada franja, int amplitudVentanaEnMinutos = 0) =>
        new("Turno", [franja], string.Empty, amplitudVentanaEnMinutos);

    private static TurnoDiarioAsignado TurnoAsignado(string streamId, DateOnly fecha, TurnoDiario turno) =>
        new(streamId, new ColaboradorProgramado("1098765432", CodigoColaborador, "Ana Perez"),
            fecha, turno, Guid.NewGuid());

    private static MarcacionAdicionada Marcacion(
        string streamId, DateTime timestamp, string tipo = "ENTRADA") =>
        new(streamId, CodigoColaborador, timestamp, tipo, Dispositivo);

    private static SedeDeMarcacionIdentificada Sede(string streamId, DateTime timestamp) =>
        new(streamId, timestamp, Dispositivo, "001", "Sede Principal", "CC-100");

    // CA-1 (empujar): la salida de madrugada vive en el dia 21 y el reparto del 21 la entrega a la vispera.
    [Fact]
    public async Task RepartirMarcaciones_EntregaLaSalidaALaVispera_CuandoCaeEnSuCoberturaNocturna()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        var entrada = new DateTime(2026, 4, 20, 21, 55, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))),
            Marcacion(Stream20, entrada));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        Then(Stream21);
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 2);
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Entrada, entrada);
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Salida, salida);
    }

    // CA-2 (recoger): la 06:05 ya estaba en el 21 y el turno del 20 llega despues; el reparto del 20 la recoge.
    [Fact]
    public async Task RepartirMarcaciones_RecogeLaMarcacionDelVecino_CuandoElTurnoLlegaDespuesDeLaMarcacion()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia20));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        Then(Stream21);
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // CA-2 (convergencia): con la copia ya entregada, un reparto adicional no emite eventos ni publica.
    [Fact]
    public async Task RepartirMarcaciones_NoEmiteEventosNiPublica_CuandoLaCopiaYaFueEntregada()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))),
            Marcacion(Stream20, salida, "SALIDA"));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // CA-3: 12x12 -- el turno nocturno A (18:00-06:00) recibe su 06:00 en su vispera.
    [Fact]
    public async Task RepartirMarcaciones_EntregaLas0600AlNocturno_Cuando12x12()
    {
        var salida = new DateTime(2026, 4, 21, 6, 0, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(18, 0), new TimeOnly(6, 0), 1))));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // CA-3: 12x12 -- el turno diurno B (06:00-18:00) conserva su 05:55; una vispera diurna no la recibe.
    [Fact]
    public async Task RepartirMarcaciones_NoEntregaLas0555ALaVisperaDiurna_Cuando12x12()
    {
        var entradaB = new DateTime(2026, 4, 21, 5, 55, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(18, 0), 0))));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(18, 0), 0))),
            Marcacion(Stream21, entradaB));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 0);
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 1);
    }

    // CA-3: turno de 24 h 07:00-07:00 -- la salida 07:05 del dia siguiente llega al dia del turno.
    [Fact]
    public async Task RepartirMarcaciones_EntregaLaSalidaAlDiaDelTurno_CuandoElTurnoEsDe24Horas()
    {
        var salida = new DateTime(2026, 4, 21, 7, 5, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(7, 0), new TimeOnly(7, 0), 1))));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // CA-3: turno 00:30-08:30 -- la entrada 23:50 del dia anterior llega al dia del turno (empujar al siguiente).
    [Fact]
    public async Task RepartirMarcaciones_EntregaLaEntradaAlDiaSiguiente_CuandoElTurnoEmpezoPasadaLaMedianoche()
    {
        var entrada = new DateTime(2026, 4, 21, 23, 50, 0);
        Given(Stream21, Marcacion(Stream21, entrada));
        Given(Stream22, TurnoAsignado(Stream22, Dia22, Turno(Franja(new TimeOnly(0, 30), new TimeOnly(8, 30), 0))));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream22, Marcacion(Stream22, entrada));
        Then(Stream21);
        And<ControlDiarioAggregateRoot, int>(Stream22, c => c.Marcaciones.Count, 1);
    }

    // CA-4: la 01:55 del siguiente (turno 02:00-10:00) no llega a un dia 07:00-16:00.
    [Fact]
    public async Task RepartirMarcaciones_NoEntrega_CuandoLaMarcacionQuedaFueraDeLaCoberturaDelVecino()
    {
        var entrada = new DateTime(2026, 4, 21, 1, 55, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(7, 0), new TimeOnly(16, 0), 0))));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(2, 0), new TimeOnly(10, 0), 0))),
            Marcacion(Stream21, entrada));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 0);
    }

    // CA-4: un vecino con stream pero sin turno no recibe nada.
    [Fact]
    public async Task RepartirMarcaciones_NoEntrega_CuandoElVecinoNoTieneTurno()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20, Marcacion(Stream20, new DateTime(2026, 4, 20, 21, 55, 0)));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // CA-4: un vecino sin stream no recibe nada y no se crea su stream.
    [Fact]
    public async Task RepartirMarcaciones_NoCreaElStreamDelVecino_CuandoElVecinoNoExiste()
    {
        Given(Stream21, Marcacion(Stream21, new DateTime(2026, 4, 21, 6, 5, 0), "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream22);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 1);
    }

    // CA-5: la cobertura posible incluye el desplazamiento maximo de la ventana de entrada.
    [Fact]
    public async Task RepartirMarcaciones_EntregaLaSalidaFueraDeLaCoberturaDeReferencia_CuandoElTurnoTieneVentana()
    {
        var entrada = new DateTime(2026, 4, 20, 23, 0, 0);
        var salida = new DateTime(2026, 4, 21, 8, 30, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(20, 0), new TimeOnly(4, 0), 1), 180)),
            Marcacion(Stream20, entrada));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Salida, salida);
    }

    // CA-6: la copia lleva la sede de la marcacion de origen.
    [Fact]
    public async Task RepartirMarcaciones_EntregaLaCopiaConSede_CuandoLaMarcacionDeOrigenTieneSede()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"), Sede(Stream21, salida));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"), Sede(Stream20, salida));
        And<ControlDiarioAggregateRoot, string?>(
            Stream20, c => c.Marcaciones.Single().CodigoSede, "001");
    }

    // CA-6: la sede llega despues de la entrega -- la copia existente sin sede se estampa.
    [Fact]
    public async Task RepartirMarcaciones_EstampaLaSedeEnLaCopia_CuandoLaCopiaYaExistiaSinSede()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))),
            Marcacion(Stream20, salida, "SALIDA"));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"), Sede(Stream21, salida));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Sede(Stream20, salida));
        And<ControlDiarioAggregateRoot, string?>(
            Stream20, c => c.Marcaciones.Single().CodigoSede, "001");
    }
}
