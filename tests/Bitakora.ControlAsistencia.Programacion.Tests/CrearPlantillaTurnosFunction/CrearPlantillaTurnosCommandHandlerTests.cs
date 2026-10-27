using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction;

public class CrearPlantillaTurnosCommandHandlerTests : CommandHandlerAsyncTest<CrearPlantillaTurnos>
{
    private const string NombrePlantilla = "Ciclo 8 dias";

    protected override ICommandHandlerAsync<CrearPlantillaTurnos> Handler =>
        new CrearPlantillaTurnosCommandHandler(EventStore);

    [Fact]
    public async Task CrearPlantillaTurnos_EmitePlantillaDeTurnosCreadaYEstableceId_CuandoPlantillaNoExiste()
    {
        Given();
        await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EmitePlantillaDeTurnosCreada_CuandoDiasDelCicloEsElMaximo()
    {
        Given();
        await WhenAsync(new CrearPlantillaTurnos(
            GuidAggregateId, NombrePlantilla, PlantillaDeTurnosCreada.MaximoDiasDelCiclo));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 42));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaRecursoYaExisteException_CuandoPlantillaYaExiste()
    {
        Given(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8));

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaAggregateException_CuandoDiasDelCicloEstaFueraDeRango()
    {
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 0));

        await act.Should().ThrowExactlyAsync<AggregateException>();
        Then();
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }
}
