// Retirar un turno del catalogo: deja de ser asignable a nuevas solicitudes.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarTurnoFunction;

public class RetirarTurnoCommandHandlerTests : CommandHandlerAsyncTest<RetirarTurno>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000500");

    protected override ICommandHandlerAsync<RetirarTurno> Handler =>
        new RetirarTurnoCommandHandler(EventStore, PrivateEventSender);

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    [Fact]
    public async Task RetirarTurno_EmiteTurnoRetiradoYDesactivaElCatalogo_CuandoElTurnoEstaActivo()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString(), TurnoRetirado.Crear(TurnoId));
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    // El retiro tambien aplica a turnos de descanso (turnos de pleno derecho).
    [Fact]
    public async Task RetirarTurno_EmiteTurnoRetirado_CuandoElTurnoEsDeDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString(), TurnoRetirado.Crear(TurnoId));
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    // Estado ya alcanzado: no-op exitoso, el handler termina sin lanzar y sin agregar eventos.
    [Fact]
    public async Task RetirarTurno_NoEmiteEventos_CuandoElTurnoYaEstaRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurno(), TurnoRetirado.Crear(TurnoId));

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    [Fact]
    public async Task RetirarTurno_LanzaRecursoNoEncontradoException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new RetirarTurno(TurnoId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{RetirarTurnoCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }

    [Fact]
    public async Task RetirarTurno_PublicaDisenoDeTurnoActualizadoRetirado_CuandoEmiteTurnoRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString(), TurnoRetirado.Crear(TurnoId));
        ThenIsPublishedPrivately(new DisenoDeTurnoActualizado(
            TurnoId, 2, "Turno Manana", false,
            [new DetalleFranjaOrdinaria(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")],
            true));
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    [Fact]
    public async Task RetirarTurno_NoPublica_CuandoElTurnoYaEstaRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurno(), TurnoRetirado.Crear(TurnoId));

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }
}
