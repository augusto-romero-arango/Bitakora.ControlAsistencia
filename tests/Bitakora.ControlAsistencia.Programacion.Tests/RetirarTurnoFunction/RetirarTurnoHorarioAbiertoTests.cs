using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarTurnoFunction;

public class RetirarTurnoHorarioAbiertoTests : CommandHandlerAsyncTest<RetirarTurno>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;

    protected override ICommandHandlerAsync<RetirarTurno> Handler =>
        new RetirarTurnoCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task RetirarTurno_EmiteTurnoRetirado_CuandoElTurnoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        await WhenAsync(new RetirarTurno(TurnoId));

        Then(TurnoId.ToString(), TurnoRetirado.Crear(TurnoId));
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }
}
