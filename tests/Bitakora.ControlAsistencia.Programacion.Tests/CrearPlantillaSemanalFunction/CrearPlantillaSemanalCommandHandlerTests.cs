// Issue #620: implementar comando CrearPlantillaSemanal con aggregate, handler y endpoint HTTP

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

    // CA-3: camino feliz -- la plantilla nace vacia, solo Id queda establecido.
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

    // CA-2 (borde inclusive) sobre el mismo canal del handler: Semanas = 6 tambien crea.
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

    // CA-4: PlantillaId ya tiene stream -> 409, sin escribir nada.
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

    // CA-4 (ultimo enunciado): un nombre ya usado por OTRA plantilla no se rechaza -- la
    // unicidad de nombre llega en #626, con la vista que hoy no existe.
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
