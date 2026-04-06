// HU-4: Implementar comando CrearTurno con aggregate, handler y endpoint HTTP

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;
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
        var eventoEsperado = TurnoCreado.Crear(comando);

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
        var eventoPrevio = TurnoCreado.Crear(comando);

        Given(eventoPrevio);

        var act = async () => await WhenAsync(comando);
        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{CrearTurnoCommandHandler.Mensajes.TurnoYaExiste}*");
    }
}
