using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;


namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

public class CrearTurnoCommandHandlerTests : CommandHandlerAsyncTest<CrearTurno>
{
    private const string NombreTurno = "Turno Manana";

    private static CrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    private static CrearTurno ComandoConUnaFranja(Guid turnoId) =>
        new(turnoId, NombreTurno, [FranjaDiurnaSimple()]);

    // Catalogo vigente que el handler ve (vista FichaTurno). Reasignarlo SIEMPRE antes de
    // WhenAsync: Handler se construye recien al ejecutar el comando.
    private FakeLectorNombresTurno _lector = new();

    // Siembra un turno ya creado en el catalogo: el evento en su propio stream (lo que el event
    // store ve) y su nombre en el lector (lo que la vista le devuelve al handler).
    private Guid SembrarTurnoEnCatalogo(string nombre)
    {
        var turnoId = new Guid("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
        var comandoPrevio = new CrearTurno(turnoId, nombre, [FranjaDiurnaSimple()]);
        Given(turnoId.ToString(), TurnoCreado.Crear(turnoId, nombre, comandoPrevio.ToDatosFranjas()));
        _lector = new FakeLectorNombresTurno(nombre);
        return turnoId;
    }

    protected override ICommandHandlerAsync<CrearTurno> Handler =>
        new CrearTurnoCommandHandler(EventStore, _lector);

    // ToString produce las franjas (sin el nombre) con el ToString() de cada FranjaOrdinaria.
    [Fact]
    public async Task DebeEmitirTurnoCreadoYEstablecerEstado_CuandoTurnoNoExiste()
    {
        var comando = ComandoConUnaFranja(GuidAggregateId);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
        And<CatalogoTurnos, string>(c => c.ToString(), "08:00-16:00");
    }

    [Fact]
    public async Task CrearTurno_NoPublicaDisenoDeTurno_CuandoCreaElTurno()
    {
        var comando = ComandoConUnaFranja(GuidAggregateId);

        Given();
        await WhenAsync(comando);

        Then(TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas()));
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearTurno_LanzaRecursoYaExisteException_CuandoTurnoYaExiste()
    {
        var comando = ComandoConUnaFranja(GuidAggregateId);
        var eventoPrevio = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given(eventoPrevio);

        var act = async () => await WhenAsync(comando);
        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearTurnoCommandHandler.Mensajes.TurnoYaExiste}*");
    }

    // La sede prearmada de cada franja del catalogo llega hasta el detalle del
    // aggregate cuando el comando la trae en algunas franjas (turno partido multi-sede).
    [Fact]
    public async Task CrearTurno_PersisteSedePorFranja_CuandoComandoTraeSedeEnAlgunasFranjas()
    {
        var sedeManana = new SedeProgramada("SEDE-SUBA", "Suba");
        var franjaConSede = new CrearTurno.Franja(
            new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sedeManana);
        var franjaSinSede = new CrearTurno.Franja(
            new TimeOnly(14, 0), new TimeOnly(22, 0), [], []);
        var comando = new CrearTurno(GuidAggregateId, "Turno Partido", [franjaConSede, franjaSinSede]);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, SedeProgramada?>(
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, sedeManana);
        And<CatalogoTurnos, SedeProgramada?>(
            c => c.ObtenerDetalle().FranjasOrdinarias[1].Sede, null);
    }

    // Un body sin franjas ni marca de descanso no es 400: el turno nace incompleto.
    [Fact]
    public async Task CrearTurno_EmiteTurnoCreadoConFranjasVaciasYEsDescansoFalso_CuandoNoTraeFranjasNiMarca()
    {
        var comando = new CrearTurno(GuidAggregateId, NombreTurno);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, []);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearTurno_EmiteTurnoCreadoConFranjasVacias_CuandoEsDescansoEsTrue()
    {
        const string nombreDescanso = "Descanso Compensatorio";
        var descripcionEsperada = Turno.Mensajes.LabelDescanso;
        var comando = new CrearTurno(GuidAggregateId, nombreDescanso, [], EsDescanso: true);
        var eventoEsperado = TurnoCreado.CrearDescanso(comando.TurnoId, comando.Nombre);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().FranjasOrdinarias.Count, 0);
        And<CatalogoTurnos, string>(c => c.ToString(), descripcionEsperada);
        And<CatalogoTurnos, string>(c => c.ObtenerDetalle().Descripcion, descripcionEsperada);
    }

    [Fact]
    public async Task CrearTurno_LanzaReglaDeNegocioDeclinadaException_CuandoNombreCoincideExactamenteConUnoDelCatalogo()
    {
        const string nombreExistente = "Limpieza mañana";
        var turnoExistenteId = SembrarTurnoEnCatalogo(nombreExistente);
        var comando = new CrearTurno(GuidAggregateId, nombreExistente, [FranjaDiurnaSimple()]);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearTurnoCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<CatalogoTurnos, string>(
            turnoExistenteId.ToString(), c => c.ToString(), "08:00-16:00");
    }

    // Normalizacion del nombre: trim + colapso de espacios + case-insensitive.
    [Fact]
    public async Task CrearTurno_LanzaReglaDeNegocioDeclinadaException_CuandoNombreDifiereSoloEnMayusculasYEspaciosDeUnoDelCatalogo()
    {
        const string nombreExistente = "Limpieza mañana";
        const string nombreConEspaciosYMayusculas = "  limpieza  MAÑANA ";
        var turnoExistenteId = SembrarTurnoEnCatalogo(nombreExistente);
        var comando = new CrearTurno(GuidAggregateId, nombreConEspaciosYMayusculas, [FranjaDiurnaSimple()]);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearTurnoCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<CatalogoTurnos, string>(
            turnoExistenteId.ToString(), c => c.ToString(), "08:00-16:00");
    }

    // Decision del experto: normalizar acentos abre falsos positivos, los acentos SON significativos.
    [Fact]
    public async Task CrearTurno_EmiteTurnoCreado_CuandoNombreDifiereSoloEnAcentosDeUnoDelCatalogo()
    {
        const string nombreExistente = "Limpieza mañana";
        const string nombreSinAcento = "Limpieza manana";
        _lector = new FakeLectorNombresTurno(nombreExistente);
        var comando = new CrearTurno(GuidAggregateId, nombreSinAcento, [FranjaDiurnaSimple()]);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given();

        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearTurno_EmiteTurnoCreado_CuandoNombreNoCoincideConNingunoDelCatalogo()
    {
        _lector = new FakeLectorNombresTurno("Limpieza mañana", "Turno Noche");
        var comando = ComandoConUnaFranja(GuidAggregateId);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given();

        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
    }

    // El extra va contenido en 06:00-14:00: las extras se programan dentro de su ordinaria
    // (invariante de catalogo-turnos.yaml). Un extra que la desborde hace fallar el arrange.
    [Fact]
    public async Task CrearTurno_EmiteTurnoCreadoConDescansoYExtra_CuandoFranjaTraeHijasComoRango()
    {
        var comando = new CrearTurno(GuidAggregateId, "Diurno",
            [new CrearTurno.Franja(
                new TimeOnly(6, 0), new TimeOnly(14, 0),
                Descansos: [new CrearTurno.Rango(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                Extras: [new CrearTurno.Rango(new TimeOnly(13, 0), new TimeOnly(14, 0))])]);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 1);
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().FranjasOrdinarias[0].Extras.Count, 1);
    }
}

internal sealed class FakeLectorNombresTurno(params string[] nombres) : ILectorNombresTurno
{
    public Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(nombres);
}
