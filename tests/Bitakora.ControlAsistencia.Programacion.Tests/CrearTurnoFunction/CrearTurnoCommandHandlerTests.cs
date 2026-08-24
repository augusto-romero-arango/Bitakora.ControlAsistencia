// HU-4: Implementar comando CrearTurno con aggregate, handler y endpoint HTTP

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;


namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

public class CrearTurnoCommandHandlerTests : CommandHandlerAsyncTest<CrearTurno>
{
    private const string NombreTurno = "Turno Manana";

    // Factory method compartido entre las clases anidadas
    private static CrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    private static CrearTurno ComandoConUnaFranja(Guid turnoId) =>
        new(turnoId, NombreTurno, [FranjaDiurnaSimple()]);

    protected override ICommandHandlerAsync<CrearTurno> Handler =>
        new CrearTurnoCommandHandler(EventStore);

    // CA-3: handler persiste evento cuando turno no existe
    // CA-1: aggregate aplica TurnoCreado y establece Id (AggregateRoot.Id = turnoId.ToString())
    // CA-2: ToString produce "{nombre} (franja1)" usando el ToString() de cada FranjaOrdinaria
    [Fact]
    public async Task DebeEmitirTurnoCreadoYEstablecerEstado_CuandoTurnoNoExiste()
    {
        var comando = ComandoConUnaFranja(GuidAggregateId);
        var eventoEsperado = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
        And<CatalogoTurnos, string>(c => c.ToString(), $"{NombreTurno} (08:00-16:00)");
    }

    // CA-4: handler lanza excepcion cuando turno ya existe (idempotencia -> 409 Conflict)
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoTurnoYaExiste()
    {
        var comando = ComandoConUnaFranja(GuidAggregateId);
        var eventoPrevio = TurnoCreado.Crear(comando.TurnoId, comando.Nombre, comando.ToDatosFranjas());

        Given(eventoPrevio);

        var act = async () => await WhenAsync(comando);
        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{CrearTurnoCommandHandler.Mensajes.TurnoYaExiste}*");
    }

    // Issue #335 CA-1: la sede prearmada de cada franja del catalogo llega hasta el detalle del
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

    // ---------- Issue #423: descanso programado (EsDescanso=true) ----------

    // CA-4: handler despacha a TurnoCreado.CrearDescanso cuando EsDescanso=true y persiste con
    // cero franjas ordinarias.
    // CA-6: el catalogo se autodescribe distinto para el descanso, sin ifs sobre el conteo de
    // franjas fuera del aggregate; TurnoProgramado.Descripcion (vista via ObtenerDetalle) hereda
    // esa misma descripcion.
    [Fact]
    public async Task CrearTurno_EmiteTurnoCreadoConFranjasVacias_CuandoEsDescansoEsTrue()
    {
        var comando = new CrearTurno(GuidAggregateId, "Descanso Compensatorio", [], EsDescanso: true);
        var eventoEsperado = TurnoCreado.CrearDescanso(comando.TurnoId, comando.Nombre);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().FranjasOrdinarias.Count, 0);
        And<CatalogoTurnos, string>(c => c.ToString(), "Descanso Compensatorio (descanso)");
        And<CatalogoTurnos, string>(
            c => c.ObtenerDetalle().Descripcion, "Descanso Compensatorio (descanso)");
    }
}
