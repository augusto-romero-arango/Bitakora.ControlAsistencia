using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using static Bitakora.ControlAsistencia.Programacion.Tests.Entities.AdvertenciasEsperadasPlantillaTurnos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaAPlantillaTurnosFunction;

public class AsignarJornadaAPlantillaTurnosCommandHandlerTests
    : CommandHandlerAsyncTest<AsignarJornadaAPlantillaTurnos>
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    // El TestStore no puebla AggregateRoot.Version: la copia que el handler guarda es 0 en este harness.
    private const long VersionEnElHarness = 0;

    protected override ICommandHandlerAsync<AsignarJornadaAPlantillaTurnos> Handler =>
        new AsignarJornadaAPlantillaTurnosCommandHandler(EventStore);

    private static LimitesJornada Limites(int semanales) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaDeTurnosCreada PlantillaCreada() =>
        PlantillaDeTurnosCreada.Crear(GuidAggregateId, "Ciclo Cocina", 8);

    private void GivenJornada(int semanales) =>
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(semanales)));

    private void GivenJornadaDesactivada(int semanales) =>
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(semanales)), new JornadaDesactivada(JornadaId));

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_EmiteJornadaAsignada_CuandoLaJornadaExiste()
    {
        Given(PlantillaCreada(), EventoSinJornada(GuidAggregateId));
        GivenJornada(42);

        await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        Then(JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness),
            EventoDiasSinTurno(GuidAggregateId, 8));
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_NoEmiteEventos_CuandoLaCopiaYaEstaAlDia()
    {
        Given(PlantillaCreada(), EventoSinJornada(GuidAggregateId),
            JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness),
            EventoDiasSinTurno(GuidAggregateId, 8));
        GivenJornada(42);

        await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        Then();
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaYaTieneLaJornadaDesactivada()
    {
        Given(PlantillaCreada(), EventoSinJornada(GuidAggregateId),
            JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness),
            EventoDiasSinTurno(GuidAggregateId, 8));
        GivenJornadaDesactivada(42);

        await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        Then();
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        GivenJornada(42);

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarJornadaAPlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoExiste()
    {
        Given(PlantillaCreada());

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarJornadaAPlantillaTurnosCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), PlantillaDeTurnosRetirada.Crear(GuidAggregateId));
        GivenJornada(42);

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarJornadaAPlantillaTurnosCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoLaJornadaEstaDesactivada()
    {
        Given(PlantillaCreada());
        GivenJornadaDesactivada(42);

        var act = async () => await WhenAsync(new AsignarJornadaAPlantillaTurnos(GuidAggregateId, JornadaId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarJornadaAPlantillaTurnosCommandHandler.Mensajes.JornadaDesactivada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, null);
    }
}
