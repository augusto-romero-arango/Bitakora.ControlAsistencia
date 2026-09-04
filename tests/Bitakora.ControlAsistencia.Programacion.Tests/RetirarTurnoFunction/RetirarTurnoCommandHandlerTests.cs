// Issue #500: retirar un turno del catalogo -- ya no asignable a nuevas solicitudes

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarTurnoFunction;

public class RetirarTurnoCommandHandlerTests : CommandHandlerAsyncTest<RetirarTurno>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000500");

    protected override ICommandHandlerAsync<RetirarTurno> Handler =>
        new RetirarTurnoCommandHandler(EventStore);

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    // CA-1
    [Fact]
    public async Task RetirarTurno_EmiteTurnoRetiradoYDesactivaElCatalogo_CuandoElTurnoEstaActivo()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString(), TurnoRetirado.Crear(TurnoId));
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    // CA-5: el retiro tambien aplica a turnos de descanso (turnos de pleno derecho, #423)
    [Fact]
    public async Task RetirarTurno_EmiteTurnoRetirado_CuandoElTurnoEsDeDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString(), TurnoRetirado.Crear(TurnoId));
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    // CA-3: idempotencia -- retirar un turno ya retirado declina sin re-emitir (CA-ADR-0030)
    [Fact]
    public async Task RetirarTurno_LanzaInvalidOperationException_CuandoElTurnoYaEstaRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurno(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new RetirarTurno(TurnoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarTurnoCommandHandler.Mensajes.TurnoYaRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    // CA-2: turno inexistente -> 404, sin escribir nada al event store
    [Fact]
    public async Task RetirarTurno_LanzaKeyNotFoundException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new RetirarTurno(TurnoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarTurnoCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }
}
