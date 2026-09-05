using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarTurnoDeDiaDePlantillaSemanalFunction;

public class QuitarTurnoDeDiaDePlantillaSemanalCommandHandlerTests
    : CommandHandlerAsyncTest<QuitarTurnoDeDiaDePlantillaSemanal>
{
    private const string NombrePlantilla = "Semana Cocina";
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    protected override ICommandHandlerAsync<QuitarTurnoDeDiaDePlantillaSemanal> Handler =>
        new QuitarTurnoDeDiaDePlantillaSemanalCommandHandler(EventStore);

    private PlantillaSemanalCreada CrearEventoPlantilla(int semanas = 2) =>
        PlantillaSemanalCreada.Crear(GuidAggregateId, NombrePlantilla, semanas);

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_EmiteDiaQuitado_CuandoElDiaTieneTurnoAsignado()
    {
        Given(CrearEventoPlantilla(),
            DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await WhenAsync(new QuitarTurnoDeDiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5)));

        Then(DiaDePlantillaSemanalQuitado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5)));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_LanzaKeyNotFoundException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(
            new QuitarTurnoDeDiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5)));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{QuitarTurnoDeDiaDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_LanzaInvalidOperationException_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        Given(CrearEventoPlantilla(semanas: 2),
            DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        var act = async () => await WhenAsync(
            new QuitarTurnoDeDiaDePlantillaSemanal(GuidAggregateId, 3, DiaSemana.Desde(5)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarTurnoDeDiaDePlantillaSemanalCommandHandler.Mensajes.SemanaFueraDeRango}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-5 (issue #623): la plantilla retirada gana a cualquier otra evaluacion del handler.
    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_LanzaInvalidOperationException_CuandoLaPlantillaEstaRetirada()
    {
        Given(CrearEventoPlantilla(),
            DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId),
            PlantillaSemanalRetirada.Crear(GuidAggregateId));

        var act = async () => await WhenAsync(
            new QuitarTurnoDeDiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarTurnoDeDiaDePlantillaSemanalCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
    }

    // Idempotencia: un dia ya vacio no es rechazo -- el handler retorna sin lanzar (CA-ADR-0030).
    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_NoEmiteEvento_CuandoElDiaYaEstaVacio()
    {
        Given(CrearEventoPlantilla());

        await WhenAsync(new QuitarTurnoDeDiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5)));

        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }
}
