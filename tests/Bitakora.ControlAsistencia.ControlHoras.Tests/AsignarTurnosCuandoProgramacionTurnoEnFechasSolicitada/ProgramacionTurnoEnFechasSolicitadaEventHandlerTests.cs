using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada;

public class ProgramacionTurnoEnFechasSolicitadaEventHandlerTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoEnFechasSolicitada>
{
    private const string CodigoColaborador = "EMP-001";
    private const string Dispositivo = "DEV-001";

    private static readonly Guid SolicitudId = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly Guid SolicitudPrevia = Guid.Parse("019600b0-0000-7000-8000-000000000002");
    private static readonly Guid JornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000aa");

    private static readonly DateOnly Dia20 = new(2026, 4, 20);
    private static readonly DateOnly Dia21 = new(2026, 4, 21);
    private static readonly DateOnly Dia22 = new(2026, 4, 22);

    private static readonly string Stream19 = $"cd:{CodigoColaborador}:20260419";
    private static readonly string Stream20 = $"cd:{CodigoColaborador}:20260420";
    private static readonly string Stream21 = $"cd:{CodigoColaborador}:20260421";
    private static readonly string Stream22 = $"cd:{CodigoColaborador}:20260422";

    private static readonly ColaboradorProgramado Colaborador = new("1098765432", CodigoColaborador, "Ana Perez");
    private static readonly ResumenColaborador ColaboradorResumen = new("1098765432", CodigoColaborador, "Ana Perez");

    private static readonly DetalleTurno TurnoDiurnoBus = new("Turno Manana",
        [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")], "");
    private static readonly TurnoDiario TurnoDiurno = new("Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")], "");

    private static readonly DetalleTurno TurnoNocturnoBus = new("Turno Noche",
        [new DetalleFranjaOrdinaria(new TimeOnly(22, 0), new TimeOnly(6, 0), 1, [], [], "")], "");
    private static readonly TurnoDiario TurnoNocturno = new("Turno Noche",
        [new FranjaProgramada(new TimeOnly(22, 0), new TimeOnly(6, 0), 1, [], [], "")], "");

    private static readonly DetalleJornada JornadaBus = new(JornadaId, 2880, 600, 0, 1);
    private static readonly JornadaProgramada Jornada = new(JornadaId, 2880, 600, 0, 1);

    // Nombre de sede largo para inflar cada foto y forzar la particion por tamano.
    private static readonly string SedePesada = new('S', 1000);

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoEnFechasSolicitada> Handler =>
        new ProgramacionTurnoEnFechasSolicitadaEventHandler(EventStore, PrivateEventSender);

    private static ProgramacionTurnoEnFechasSolicitada Solicitud(DetalleTurno turno, params DateOnly[] fechas) =>
        new(SolicitudId, ColaboradorResumen, fechas, turno, JornadaBus);

    private static TurnoDiarioAsignado Asignado(
        string streamId, DateOnly fecha, TurnoDiario turno, Guid solicitudId, JornadaProgramada? jornada = null) =>
        new(streamId, Colaborador, fecha, turno, solicitudId, jornada);

    private static MarcacionAdicionada Marcacion(string streamId, DateTime timestamp, string tipo = "ENTRADA") =>
        new(streamId, CodigoColaborador, timestamp, tipo, Dispositivo);

    private static CierreDeLaVisperaActualizado Cierre(string streamId, DateTime? cierre) =>
        CierreDeLaVisperaActualizado.Crear(streamId, cierre);

    private static object[] MarcacionesConSede(string streamId, DateOnly dia, int cantidad, string nombreSede) =>
        Enumerable.Range(0, cantidad)
            .Select(i => dia.ToDateTime(new TimeOnly(6, 0)).AddMinutes(i))
            .SelectMany(instante => new object[]
            {
                Marcacion(streamId, instante),
                new SedeDeMarcacionIdentificada(streamId, instante, Dispositivo, "001", nombreSede, "CC-100")
            })
            .ToArray();

    private IEnumerable<T> Publicados<T>() where T : class, IPrivateEvent =>
        PrivateEventSender.PublishedWithOptions.SelectMany(p => p.Events).OfType<T>();

    private IEnumerable<PublishOptions> OpcionesDe<T>() where T : class, IPrivateEvent =>
        PrivateEventSender.PublishedWithOptions.Where(p => p.Events.OfType<T>().Any()).Select(p => p.Options);

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_AsignaElTurnoEnTodosLosDias_CuandoUnosDiasNoTienenStream()
    {
        Given(Stream21, Asignado(Stream21, Dia21, TurnoNocturno, SolicitudPrevia));

        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia20, Dia21));

        Then(Stream20, Asignado(Stream20, Dia20, TurnoDiurno, SolicitudId, Jornada));
        Then(Stream21,
            Asignado(Stream21, Dia21, TurnoDiurno, SolicitudId, Jornada),
            Cierre(Stream21, new DateTime(2026, 4, 20, 16, 0, 0)));
        And<ControlDiarioAggregateRoot, Guid>(Stream20, c => c.UltimaSolicitudId, SolicitudId);
        And<ControlDiarioAggregateRoot, Guid>(Stream21, c => c.UltimaSolicitudId, SolicitudId);
        And<ControlDiarioAggregateRoot, string?>(Stream21, c => c.DetalleTurno!.Nombre, "Turno Manana");
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_NoEmiteEventosEnElDia_CuandoYaTieneLaMismaSolicitudYElMismoTurno()
    {
        Given(Stream20, Asignado(Stream20, Dia20, TurnoDiurno, SolicitudId, Jornada));

        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia20, Dia21));

        Then(Stream20);
        Then(Stream21,
            Asignado(Stream21, Dia21, TurnoDiurno, SolicitudId, Jornada),
            Cierre(Stream21, new DateTime(2026, 4, 20, 16, 0, 0)));
        And<ControlDiarioAggregateRoot, Guid>(Stream20, c => c.UltimaSolicitudId, SolicitudId);
        Publicados<DiasDepurados>().Single().Dias.Select(d => d.Fecha).Should().Equal(Dia20, Dia21);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_AsignaDeNuevo_CuandoLaMismaSolicitudTraeOtroTurno()
    {
        Given(Stream20, Asignado(Stream20, Dia20, TurnoNocturno, SolicitudId, Jornada));

        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia20));

        Then(Stream20, Asignado(Stream20, Dia20, TurnoDiurno, SolicitudId, Jornada));
        And<ControlDiarioAggregateRoot, string?>(Stream20, c => c.DetalleTurno!.Nombre, "Turno Manana");
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_RepartePorLaCoberturaDeCadaDia_CuandoLosDiasSonNocturnos()
    {
        var entrada = new DateTime(2026, 4, 20, 21, 55, 0);
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20, Marcacion(Stream20, entrada));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(Solicitud(TurnoNocturnoBus, Dia20, Dia21));

        Then(Stream20,
            Asignado(Stream20, Dia20, TurnoNocturno, SolicitudId, Jornada),
            Marcacion(Stream20, salida, "SALIDA"));
        Then(Stream21,
            Asignado(Stream21, Dia21, TurnoNocturno, SolicitudId, Jornada),
            Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 2);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_EscribeYPublicaElVecinoDeAfuera_CuandoElRepartoLoCambia()
    {
        var entrada = new DateTime(2026, 4, 20, 21, 55, 0);
        var salida = new DateTime(2026, 4, 21, 6, 5, 0);
        Given(Stream20,
            Asignado(Stream20, Dia20, TurnoNocturno, SolicitudPrevia, Jornada),
            Marcacion(Stream20, entrada));
        Given(Stream21, Marcacion(Stream21, salida, "SALIDA"));

        await WhenAsync(Solicitud(TurnoNocturnoBus, Dia21));

        Then(Stream20, Marcacion(Stream20, salida, "SALIDA"));
        Then(Stream21,
            Asignado(Stream21, Dia21, TurnoNocturno, SolicitudId, Jornada),
            Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)));
        Publicados<DiaDepurado>().Should().ContainSingle().Which.Fecha.Should().Be(Dia20);
        OpcionesDe<DiaDepurado>().Should().ContainSingle().Which.GroupId.Should().Be(CodigoColaborador);
        Publicados<DiasDepurados>().Single().Dias.Select(d => d.Fecha).Should().Equal(Dia21);
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 2);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_NoPublicaNiCreaVecinos_CuandoElRepartoNoLosCambia()
    {
        Given(Stream20, Asignado(Stream20, Dia20, TurnoDiurno, SolicitudPrevia, Jornada));

        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia21));

        Then(Stream20);
        Then(Stream21,
            Asignado(Stream21, Dia21, TurnoDiurno, SolicitudId, Jornada),
            Cierre(Stream21, new DateTime(2026, 4, 20, 16, 0, 0)));
        Publicados<DiaDepurado>().Should().BeEmpty();
        PrivateEventSender.PublishedWithOptions.Should().ContainSingle();
        And<ControlDiarioAggregateRoot, Guid>(Stream20, c => c.UltimaSolicitudId, SolicitudPrevia);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_PublicaUnaFotoPorDiaEnOrdenDeFecha_ConGroupIdDelColaborador()
    {
        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia20, Dia21, Dia22));

        var mensaje = Publicados<DiasDepurados>().Should().ContainSingle().Subject;
        mensaje.CodigoColaborador.Should().Be(CodigoColaborador);
        mensaje.Dias.Select(d => d.Fecha).Should().Equal(Dia20, Dia21, Dia22);
        mensaje.Dias.Should().OnlyContain(d => d.NombreTurno == "Turno Manana");
        OpcionesDe<DiasDepurados>().Should().ContainSingle().Which.GroupId.Should().Be(CodigoColaborador);
        Publicados<DiaDepurado>().Should().BeEmpty();
        Then(Stream20, Asignado(Stream20, Dia20, TurnoDiurno, SolicitudId, Jornada));
        And<ControlDiarioAggregateRoot, Guid>(Stream22, c => c.UltimaSolicitudId, SolicitudId);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_PartePorTamano_CuandoLasFotosSuperanElTopeDe192Kb()
    {
        Given(Stream20, MarcacionesConSede(Stream20, Dia20, 250, SedePesada));
        Given(Stream21, MarcacionesConSede(Stream21, Dia21, 250, SedePesada));

        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia20, Dia21));

        var mensajes = Publicados<DiasDepurados>().ToList();
        mensajes.Should().HaveCount(2);
        mensajes.Select(m => m.Dias.Single().Fecha).Should().Equal(Dia20, Dia21);
        And<ControlDiarioAggregateRoot, int>(Stream20, c => c.Marcaciones.Count, 250);
        Then(Stream20, Asignado(Stream20, Dia20, TurnoDiurno, SolicitudId, Jornada));
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_DejaCadaDiaConLasMarcacionesDeSuCobertura_CuandoSonTresDiasNocturnosConsecutivos()
    {
        var entrada20 = new DateTime(2026, 4, 20, 21, 55, 0);
        var salida21 = new DateTime(2026, 4, 21, 6, 5, 0);
        var entrada21 = new DateTime(2026, 4, 21, 21, 55, 0);
        var salida22 = new DateTime(2026, 4, 22, 6, 5, 0);
        var entrada22 = new DateTime(2026, 4, 22, 21, 55, 0);
        Given(Stream20, Marcacion(Stream20, entrada20));
        Given(Stream21, Marcacion(Stream21, salida21, "SALIDA"), Marcacion(Stream21, entrada21));
        Given(Stream22, Marcacion(Stream22, salida22, "SALIDA"), Marcacion(Stream22, entrada22));

        await WhenAsync(Solicitud(TurnoNocturnoBus, Dia20, Dia21, Dia22));

        Then(Stream20,
            Asignado(Stream20, Dia20, TurnoNocturno, SolicitudId, Jornada),
            Marcacion(Stream20, salida21, "SALIDA"));
        Then(Stream21,
            Asignado(Stream21, Dia21, TurnoNocturno, SolicitudId, Jornada),
            Cierre(Stream21, new DateTime(2026, 4, 21, 6, 0, 0)),
            Marcacion(Stream21, salida22, "SALIDA"));
        Then(Stream22,
            Asignado(Stream22, Dia22, TurnoNocturno, SolicitudId, Jornada),
            Cierre(Stream22, new DateTime(2026, 4, 22, 6, 0, 0)));
        var franjas = Publicados<DiasDepurados>().Single().Dias
            .ToDictionary(d => d.Fecha, d => d.Franjas.Single());
        (franjas[Dia20].Entrada, franjas[Dia20].Salida).Should().Be((entrada20, salida21));
        (franjas[Dia21].Entrada, franjas[Dia21].Salida).Should().Be((entrada21, salida22));
        And<ControlDiarioAggregateRoot, int>(Stream21, c => c.Marcaciones.Count, 3);
    }

    [Fact]
    public async Task ProgramacionTurnoEnFechasSolicitada_AgrupaVariosDiasPorMensaje_HastaElTopeDe192Kb()
    {
        Given(Stream20, MarcacionesConSede(Stream20, Dia20, 70, SedePesada));
        Given(Stream21, MarcacionesConSede(Stream21, Dia21, 70, SedePesada));
        Given(Stream22, MarcacionesConSede(Stream22, Dia22, 70, SedePesada));

        await WhenAsync(Solicitud(TurnoDiurnoBus, Dia20, Dia21, Dia22));

        var mensajes = Publicados<DiasDepurados>().ToList();
        mensajes.Select(m => m.Dias.Count).Should().Equal(2, 1);
        mensajes.SelectMany(m => m.Dias).Select(d => d.Fecha).Should().Equal(Dia20, Dia21, Dia22);
        OpcionesDe<DiasDepurados>().Should().OnlyContain(o => o.GroupId == CodigoColaborador);
        Then(Stream22,
            Asignado(Stream22, Dia22, TurnoDiurno, SolicitudId, Jornada),
            Cierre(Stream22, new DateTime(2026, 4, 21, 16, 0, 0)));
        And<ControlDiarioAggregateRoot, int>(Stream22, c => c.Marcaciones.Count, 70);
    }
}
