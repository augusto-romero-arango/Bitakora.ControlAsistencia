using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction.CommandHandler;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
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
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 10, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));
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
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 10, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // CA-2/CA-3: cada DiaDepurado del reparto (dia y vecinos) sale con GroupId del colaborador, nunca sin opciones.
    [Fact]
    public async Task RepartirMarcaciones_PublicaDiaDepuradoConGroupIdDelColaborador_CuandoRecogeLaMarcacionDelVecino()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia20));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 10, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));
        PrivateEventSender.Events.Should().BeEmpty();
        PrivateEventSender.PublishedWithOptions.Should().OnlyContain(p => p.Options.GroupId == CodigoColaborador);
        PrivateEventSender.PublishedWithOptions.SelectMany(p => p.Events).OfType<DiaDepurado>()
            .Select(d => d.Fecha).Should().BeEquivalentTo([Dia20, Dia21]);
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
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"), Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 10, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
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
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 20, 18, 0, 0)));
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
        Then(Stream21, Cobertura(Stream21, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 22, 30, 0)));
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
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 20, 16, 0, 0)));
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
        Then(Stream21);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
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
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
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

    // CA-6: la copia que ya tenia otra sede se re-estampa con la de la marcacion de origen.
    [Fact]
    public async Task RepartirMarcaciones_ReestampaLaSedeDeLaCopia_CuandoLaCopiaTieneOtraSede()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))),
            Marcacion(Stream20, salida, "SALIDA"),
            new SedeDeMarcacionIdentificada(Stream20, salida, Dispositivo, "002", "Sede Norte", "CC-200"));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"), Sede(Stream21, salida));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Sede(Stream20, salida));
        And<ControlDiarioAggregateRoot, string?>(
            Stream20, c => c.Marcaciones.Single().CodigoSede, "001");
    }

    // CA-2 (ambos repartos): dos repartos concurrentes persistieron la misma copia; el replay conserva
    // una sola y un reparto adicional no emite eventos.
    [Fact]
    public async Task RepartirMarcaciones_ConservaUnaSolaCopia_CuandoDosRepartosConcurrentesLaEntregaron()
    {
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))),
            Marcacion(Stream20, salida, "SALIDA"),
            Marcacion(Stream20, salida, "SALIDA"));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"), Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 10, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia20));

        Then(Stream20);
        Then(Stream21);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 1);
    }

    // Una marcacion que el dia recibio de otro (la 01:00 del 21, copiada al 20) no se re-reparte,
    // aunque caiga en la cobertura del vecino (turno de 24 h del 19 que cubre hasta el 21 a las 03:00).
    [Fact]
    public async Task RepartirMarcaciones_NoReparteLasCopiasRecibidasDeOtroDia()
    {
        var copiaDel21 = new DateTime(2026, 4, 21, 1, 0, 0);
        Given(Stream19, TurnoAsignado(Stream19, Dia20.AddDays(-1), Turno(Franja(new TimeOnly(23, 0), new TimeOnly(23, 0), 1))));
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))),
            Marcacion(Stream20, copiaDel21));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia20));

        Then(Stream19);
        Then(Stream20, Cierre(Stream20, new DateTime(2026, 4, 20, 23, 0, 0)));
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, int>(Stream19, c => c.Marcaciones.Count, 0);
    }

    private static CierreDeLaVisperaActualizado Cierre(string streamId, DateTime? cierre) =>
        CierreDeLaVisperaActualizado.Crear(streamId, cierre);

    private static CoberturaVecinaActualizada Cobertura(string streamId, DiaVecino vecino, DateTime? limite) =>
        CoberturaVecinaActualizada.Crear(streamId, vecino, limite);

    private static readonly ColaboradorProgramado Colaborador = new("1098765432", CodigoColaborador, "Ana Perez");

    // CA-5: al asignar, cada dia recibe la cobertura de su vecino.
    [Fact]
    public async Task RepartirMarcaciones_InformaLaCoberturaALosVecinos_CuandoAmbosTienenTurno()
    {
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))));
        Given(Stream21, TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 4, 0, 0)));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 0);
    }

    // CA-5: cancelar el turno del dia deja al vecino con limite null.
    [Fact]
    public async Task RepartirMarcaciones_InformaLimiteNull_CuandoElVecinoCancelaSuTurno()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 4, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)),
            TurnoDiarioCancelado.Crear(Stream21, Colaborador, Dia21, Guid.NewGuid()));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Cobertura(Stream20, DiaVecino.DiaSiguiente, null));
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 0);
    }

    // CA-5: una ausencia en el dia cancela su cobertura; el vecino recibe null.
    [Fact]
    public async Task RepartirMarcaciones_InformaLimiteNull_CuandoElVecinoTieneAusencia()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 4, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)),
            AusenciaDiariaAsignada.Crear(Stream21, Colaborador, Dia21, Guid.NewGuid(), "VACACIONES"));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Cobertura(Stream20, DiaVecino.DiaSiguiente, null));
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 0);
    }

    // CA-5: un reparto sin cambios no emite eventos.
    [Fact]
    public async Task RepartirMarcaciones_NoEmiteEventos_CuandoLasCoberturasYaEstanInformadas()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 4, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)),
            Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 0);
    }

    // CA-2: el turno del dia pasa a 08:00-16:00 (cobertura desde 06:00): la vispera recibe el limite
    // nuevo y la 04:30 que ya tenia copiada deja de ser ambigua.
    [Fact]
    public async Task RepartirMarcaciones_LaVisperaTomaLaSalida_CuandoElDiaSiguienteCambiaASuTurnoDeLas0800()
    {
        var salida = new DateTime(2026, 4, 21, 4, 30, 0);
        var entradaVispera = new DateTime(2026, 4, 20, 16, 55, 0);
        var entradaDia = new DateTime(2026, 4, 21, 8, 5, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            Marcacion(Stream20, entradaVispera),
            Marcacion(Stream20, salida, "SALIDA"),
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 4, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(8, 0), new TimeOnly(16, 0), 0))),
            Marcacion(Stream21, salida, "SALIDA"),
            Marcacion(Stream21, entradaDia),
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 6, 0, 0)));
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Entrada, entradaVispera);
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Salida, salida);
        And<ControlDiarioAggregateRoot, DateTime?>(Stream21, c => c.ControlesDeFranja.Single().Entrada, entradaDia);
    }

    // CA-4: la vispera con ventana entro a las 23:00 (fin efectivo 07:00): informa su cobertura hasta
    // las 11:00 y la 08:55 queda ambigua en ambos dias.
    [Fact]
    public async Task RepartirMarcaciones_InformaLaCoberturaHastaLas1100_CuandoLaVisperaConVentanaEntroALas2300()
    {
        var marcacionDelDia = new DateTime(2026, 4, 21, 8, 55, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(20, 0), new TimeOnly(4, 0), 1), 180)),
            Marcacion(Stream20, new DateTime(2026, 4, 20, 23, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(9, 0), new TimeOnly(17, 0), 0))),
            Marcacion(Stream21, marcacionDelDia));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20,
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 7, 0, 0)),
            Marcacion(Stream20, marcacionDelDia));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 11, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 4, 0, 0)));
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Salida, null);
        And<ControlDiarioAggregateRoot, DateTime?>(Stream21, c => c.ControlesDeFranja.Single().Entrada, null);
    }

    // CA-4: la misma vispera entro a las 20:00 (sin desplazamiento): su cobertura llega hasta las 08:00
    // y la 08:55 es la entrada del dia.
    [Fact]
    public async Task RepartirMarcaciones_ElDiaTomaLa0855ComoEntrada_CuandoLaVisperaConVentanaEntroALas2000()
    {
        var marcacionDelDia = new DateTime(2026, 4, 21, 8, 55, 0);
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(20, 0), new TimeOnly(4, 0), 1), 180)),
            Marcacion(Stream20, new DateTime(2026, 4, 20, 20, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(9, 0), new TimeOnly(17, 0), 0))),
            Marcacion(Stream21, marcacionDelDia));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20,
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 7, 0, 0)),
            Marcacion(Stream20, marcacionDelDia));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 8, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 4, 0, 0)));
        And<ControlDiarioAggregateRoot, DateTime?>(Stream20, c => c.ControlesDeFranja.Single().Salida, null);
        And<ControlDiarioAggregateRoot, DateTime?>(Stream21, c => c.ControlesDeFranja.Single().Entrada, marcacionDelDia);
    }

    private static string PertenenciasDe(ControlDiarioAggregateRoot c) =>
        string.Join("|", c.CrearDiaDepurado().Marcaciones.Select(m => m.Pertenencia ?? "null"));

    private static TurnoDiario Descanso() => new("Descanso", [], string.Empty, 0);

    private void DadaLaVisperaNocturna() =>
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))));

    // CA-1: dia de descanso tras una vispera 17:00-01:00 (cobertura hasta las 05:00).
    [Fact]
    public async Task RepartirMarcaciones_MarcaLaSalidaDeTurnoVecino_CuandoElDiaEsDescanso()
    {
        var madrugada = new DateTime(2026, 4, 21, 1, 0, 0);
        DadaLaVisperaNocturna();
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Descanso()),
            Marcacion(Stream21, madrugada, "SALIDA"),
            Marcacion(Stream21, new DateTime(2026, 4, 21, 10, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, madrugada, "SALIDA"));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, string>(Stream21, PertenenciasDe, "DeTurnoVecino|null");
    }

    // CA-1: dia sin programar.
    [Fact]
    public async Task RepartirMarcaciones_MarcaLaSalidaDeTurnoVecino_CuandoElDiaNoEstaProgramado()
    {
        var madrugada = new DateTime(2026, 4, 21, 1, 0, 0);
        DadaLaVisperaNocturna();
        Given(Stream21,
            Marcacion(Stream21, madrugada, "SALIDA"),
            Marcacion(Stream21, new DateTime(2026, 4, 21, 10, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, madrugada, "SALIDA"));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, string>(Stream21, PertenenciasDe, "DeTurnoVecino|null");
    }

    // CA-1: dia con ausencia (cubre el turno sin reemplazarlo).
    [Fact]
    public async Task RepartirMarcaciones_MarcaLaSalidaDeTurnoVecino_CuandoElDiaTieneAusencia()
    {
        var madrugada = new DateTime(2026, 4, 21, 1, 0, 0);
        DadaLaVisperaNocturna();
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            AusenciaDiariaAsignada.Crear(Stream21, Colaborador, Dia21, Guid.NewGuid(), "VACACIONES"),
            Marcacion(Stream21, madrugada, "SALIDA"),
            Marcacion(Stream21, new DateTime(2026, 4, 21, 10, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Marcacion(Stream20, madrugada, "SALIDA"));
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)), Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));
        And<ControlDiarioAggregateRoot, string>(Stream21, PertenenciasDe, "DeTurnoVecino|null");
    }

    // CA-2: el dia siguiente con turno 00:00-08:00 (cobertura desde las 22:00).
    [Fact]
    public async Task RepartirMarcaciones_MarcaLas2300DeTurnoVecino_CuandoElDiaSiguienteEmpiezaALas0000()
    {
        var noche = new DateTime(2026, 4, 21, 23, 0, 0);
        Given(Stream21, TurnoAsignado(Stream21, Dia21, Descanso()), Marcacion(Stream21, noche));
        Given(Stream22, TurnoAsignado(Stream22, Dia22, Turno(Franja(new TimeOnly(0, 0), new TimeOnly(8, 0), 0))));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream22, Marcacion(Stream22, noche));
        Then(Stream21, Cobertura(Stream21, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 22, 0, 0)));
        And<ControlDiarioAggregateRoot, string>(Stream21, PertenenciasDe, "DeTurnoVecino");
    }

    // CA-3: el vecino cancela su turno; el limite ya registrado pasa a null y la 01:00 queda propia.
    [Fact]
    public async Task RepartirMarcaciones_InformaLimiteNullYQuitaLaPertenencia_CuandoLaVisperaCancelaSuTurno()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            TurnoDiarioCancelado.Crear(Stream20, Colaborador, Dia20, Guid.NewGuid()));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Descanso()),
            Marcacion(Stream21, new DateTime(2026, 4, 21, 1, 0, 0), "SALIDA"),
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 5, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21, Cobertura(Stream21, DiaVecino.Vispera, null));
        And<ControlDiarioAggregateRoot, string>(Stream21, PertenenciasDe, "null");
    }

    // CA-4: la cobertura de la vispera 06:00-14:00 termina a las 18:00 del 20, antes del dia.
    [Fact]
    public async Task RepartirMarcaciones_NoRegistraLimite_CuandoLaCoberturaDeLaVisperaNoAlcanzaElDia()
    {
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Descanso()),
            Marcacion(Stream21, new DateTime(2026, 4, 21, 10, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 20, 14, 0, 0)));
        And<ControlDiarioAggregateRoot, string>(Stream21, PertenenciasDe, "null");
    }

    // CA-1: el solape oculta la cobertura vecina (guard de Invade) pero el dia igual conoce el cierre de la vispera.
    [Fact]
    public async Task RepartirMarcaciones_InformaElCierreDeLaVispera_CuandoLosTurnosSeSolapan()
    {
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), 1))));
        Given(Stream21, TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(4, 0), new TimeOnly(12, 0), 0))));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21, Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }

    // CA-3: la vispera cancela su turno y el dia ya tenia cierre: se apaga con null.
    [Fact]
    public async Task RepartirMarcaciones_InformaCierreNull_CuandoLaVisperaCancelaSuTurno()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            TurnoDiarioCancelado.Crear(Stream20, Colaborador, Dia20, Guid.NewGuid()));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream21, Cierre(Stream21, null));
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }

    // CA-3: la vispera pasa a descanso y el dia ya tenia cierre: se apaga con null.
    [Fact]
    public async Task RepartirMarcaciones_InformaCierreNull_CuandoLaVisperaPasaADescanso()
    {
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Descanso()));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream21, Cierre(Stream21, null));
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }

    // CA-3: la vispera recibe una ausencia y el dia ya tenia cierre: se apaga con null.
    [Fact]
    public async Task RepartirMarcaciones_InformaCierreNull_CuandoLaVisperaRecibeUnaAusencia()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(17, 0), new TimeOnly(1, 0), 1))),
            AusenciaDiariaAsignada.Crear(Stream20, Colaborador, Dia20, Guid.NewGuid(), "VACACIONES"));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))),
            Cierre(Stream21, new DateTime(2026, 4, 21, 1, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream21, Cierre(Stream21, null));
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }

    // CA-3: sin cierre previo, una vispera sin trabajo no emite nada en el dia.
    [Fact]
    public async Task RepartirMarcaciones_NoEmiteCierre_CuandoLaVisperaNoTrabajaYElDiaNoTeniaCierre()
    {
        Given(Stream20, TurnoAsignado(Stream20, Dia20, Descanso()));
        Given(Stream21, TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), 0))));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }

    // CA-4: con ventana de entrada y marcacion de entrada, el cierre informado sigue siendo el de referencia.
    [Fact]
    public async Task RepartirMarcaciones_InformaElCierreDeReferencia_CuandoLaVisperaConVentanaYaMarcoEntrada()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(20, 0), new TimeOnly(4, 0), 1), 120)),
            Marcacion(Stream20, new DateTime(2026, 4, 20, 21, 30, 0)));
        Given(Stream21, TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(9, 0), new TimeOnly(17, 0), 0))));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20, Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 7, 0, 0)));
        Then(Stream21,
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 9, 30, 0)),
            Cierre(Stream21, new DateTime(2026, 4, 21, 4, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }

    // CA-4: marcar la entrada no reemite el cierre ya informado.
    [Fact]
    public async Task RepartirMarcaciones_NoReemiteElCierre_CuandoLaVisperaMarcaDespuesDeInformado()
    {
        Given(Stream20,
            TurnoAsignado(Stream20, Dia20, Turno(Franja(new TimeOnly(20, 0), new TimeOnly(4, 0), 1), 120)),
            Marcacion(Stream20, new DateTime(2026, 4, 20, 21, 30, 0)),
            Cobertura(Stream20, DiaVecino.DiaSiguiente, new DateTime(2026, 4, 21, 7, 0, 0)));
        Given(Stream21,
            TurnoAsignado(Stream21, Dia21, Turno(Franja(new TimeOnly(9, 0), new TimeOnly(17, 0), 0))),
            Cobertura(Stream21, DiaVecino.Vispera, new DateTime(2026, 4, 21, 9, 30, 0)),
            Cierre(Stream21, new DateTime(2026, 4, 21, 4, 0, 0)));

        await WhenAsync(new RepartirMarcaciones(CodigoColaborador, Dia21));

        Then(Stream20);
        Then(Stream21);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 0);
    }
}
