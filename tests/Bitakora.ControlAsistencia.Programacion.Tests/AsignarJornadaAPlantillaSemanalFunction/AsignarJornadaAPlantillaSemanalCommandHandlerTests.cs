using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaAPlantillaSemanalFunction;

public class AsignarJornadaAPlantillaSemanalCommandHandlerTests
    : CommandHandlerAsyncTest<AsignarJornadaAPlantillaSemanal>
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    // El TestStore reconstruye los aggregates aplicando eventos y no puebla AggregateRoot.Version:
    // la version que el handler copia a la plantilla es 0 en este harness (supuesto no verificado
    // del issue #867; la comparacion de versiones se prueba en PlantillaSemanalTurnosJornadaTests).
    private const long VersionEnElHarness = 0;

    protected override ICommandHandlerAsync<AsignarJornadaAPlantillaSemanal> Handler =>
        new AsignarJornadaAPlantillaSemanalCommandHandler(EventStore);

    private static LimitesJornada Limites(int semanales) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaSemanalCreada PlantillaCreada() =>
        PlantillaSemanalCreada.Crear(GuidAggregateId, "Semana Cocina", 2);

    private void GivenJornada(int semanales) =>
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(semanales)));

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_EmiteJornadaAsignada_CuandoLaJornadaExiste()
    {
        Given(PlantillaCreada());
        GivenJornada(42);

        await WhenAsync(new AsignarJornadaAPlantillaSemanal(GuidAggregateId, JornadaId));

        Then(JornadaDePlantillaSemanalAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness));
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_NoEmiteEventos_CuandoLaCopiaYaEstaAlDia()
    {
        Given(PlantillaCreada(),
            JornadaDePlantillaSemanalAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness));
        GivenJornada(42);

        await WhenAsync(new AsignarJornadaAPlantillaSemanal(GuidAggregateId, JornadaId));

        Then();
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        GivenJornada(42);

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaSemanal(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarJornadaAPlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoExiste()
    {
        Given(PlantillaCreada());

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaSemanal(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarJornadaAPlantillaSemanalCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), PlantillaSemanalRetirada.Crear(GuidAggregateId));
        GivenJornada(42);

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaSemanal(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarJornadaAPlantillaSemanalCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, null);
    }
}
