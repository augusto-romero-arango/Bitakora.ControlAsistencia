using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction;

public class CrearPlantillaSemanalCommandHandlerTests : CommandHandlerAsyncTest<CrearPlantillaSemanal>
{
    private const string NombrePlantilla = "Semana Cocina";

    protected override ICommandHandlerAsync<CrearPlantillaSemanal> Handler =>
        new CrearPlantillaSemanalCommandHandler(EventStore);

    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreadaYEstableceId_CuandoPlantillaNoExiste()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreada_CuandoSemanasEsElMaximoPermitido()
    {
        var comando = new CrearPlantillaSemanal(
            GuidAggregateId, NombrePlantilla, PlantillaSemanalCreada.MaximoSemanas);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaSemanal_LanzaInvalidOperationException_CuandoPlantillaYaExiste()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoPrevio = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given(eventoPrevio);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // Un nombre repetido NO se rechaza todavia: la unicidad exige la vista de plantillas, que aun
    // no existe.
    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreada_CuandoNombreYaFueUsadoPorOtraPlantilla()
    {
        var otraPlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000621");
        Given(otraPlantillaId.ToString(), PlantillaSemanalCreada.Crear(otraPlantillaId, NombrePlantilla, 3));

        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }
}
