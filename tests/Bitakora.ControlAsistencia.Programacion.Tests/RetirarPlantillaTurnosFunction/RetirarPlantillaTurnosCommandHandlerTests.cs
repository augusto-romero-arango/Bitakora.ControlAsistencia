using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarPlantillaTurnosFunction;

public class RetirarPlantillaTurnosCommandHandlerTests : CommandHandlerAsyncTest<RetirarPlantillaTurnos>
{
    private const string NombrePlantilla = "Ciclo 8 dias";
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001283");

    protected override ICommandHandlerAsync<RetirarPlantillaTurnos> Handler =>
        new RetirarPlantillaTurnosCommandHandler(EventStore);

    private static PlantillaDeTurnosCreada CrearEventoPlantilla(int dias = 8) =>
        PlantillaDeTurnosCreada.Crear(PlantillaId, NombrePlantilla, dias);

    [Fact]
    public async Task RetirarPlantillaTurnos_EmitePlantillaDeTurnosRetirada_CuandoLaPlantillaEstaActiva()
    {
        Given(PlantillaId.ToString(), CrearEventoPlantilla());

        await WhenAsync(new RetirarPlantillaTurnos(PlantillaId));

        Then(PlantillaId.ToString(), PlantillaDeTurnosRetirada.Crear(PlantillaId));
        And<PlantillaTurnos, string>(PlantillaId.ToString(), p => p.Id, PlantillaId.ToString());
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(new RetirarPlantillaTurnos(PlantillaId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{RetirarPlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(PlantillaId.ToString());
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_NoEmiteEvento_CuandoLaPlantillaYaEstaRetirada()
    {
        Given(PlantillaId.ToString(), CrearEventoPlantilla(), PlantillaDeTurnosRetirada.Crear(PlantillaId));

        await WhenAsync(new RetirarPlantillaTurnos(PlantillaId));

        Then(PlantillaId.ToString());
        And<PlantillaTurnos, string>(PlantillaId.ToString(), p => p.Id, PlantillaId.ToString());
    }
}
