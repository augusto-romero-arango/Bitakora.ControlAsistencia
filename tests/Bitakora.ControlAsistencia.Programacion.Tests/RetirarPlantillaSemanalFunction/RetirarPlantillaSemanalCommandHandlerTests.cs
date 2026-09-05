// Issue #623: retirar una plantilla semanal -- ya no es usable ni editable

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarPlantillaSemanalFunction;

public class RetirarPlantillaSemanalCommandHandlerTests : CommandHandlerAsyncTest<RetirarPlantillaSemanal>
{
    private const string NombrePlantilla = "Semana Cocina";
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000623");

    protected override ICommandHandlerAsync<RetirarPlantillaSemanal> Handler =>
        new RetirarPlantillaSemanalCommandHandler(EventStore);

    private static PlantillaSemanalCreada CrearEventoPlantilla(int semanas = 2) =>
        PlantillaSemanalCreada.Crear(PlantillaId, NombrePlantilla, semanas);

    // CA-4
    [Fact]
    public async Task RetirarPlantillaSemanal_EmitePlantillaSemanalRetirada_CuandoLaPlantillaEstaActiva()
    {
        Given(PlantillaId.ToString(), CrearEventoPlantilla());

        await WhenAsync(new RetirarPlantillaSemanal(PlantillaId));

        Then(PlantillaId.ToString(), PlantillaSemanalRetirada.Crear(PlantillaId));
        And<PlantillaSemanalTurnos, string>(PlantillaId.ToString(), p => p.Id, PlantillaId.ToString());
    }

    // CA-4: plantilla sin stream -> 404
    [Fact]
    public async Task RetirarPlantillaSemanal_LanzaKeyNotFoundException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(new RetirarPlantillaSemanal(PlantillaId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarPlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(PlantillaId.ToString());
    }

    // CA-4: idempotencia (harness#850) -- retirar una plantilla ya retirada no re-emite ni lanza.
    [Fact]
    public async Task RetirarPlantillaSemanal_NoEmiteEvento_CuandoLaPlantillaYaEstaRetirada()
    {
        Given(PlantillaId.ToString(), CrearEventoPlantilla(), PlantillaSemanalRetirada.Crear(PlantillaId));

        await WhenAsync(new RetirarPlantillaSemanal(PlantillaId));

        Then(PlantillaId.ToString());
        And<PlantillaSemanalTurnos, string>(PlantillaId.ToString(), p => p.Id, PlantillaId.ToString());
    }
}
