using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using static Bitakora.ControlAsistencia.Programacion.Tests.Entities.AdvertenciasEsperadasPlantillaTurnos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarJornadaDePlantillaTurnosFunction;

public class QuitarJornadaDePlantillaTurnosCommandHandlerTests
    : CommandHandlerAsyncTest<QuitarJornadaDePlantillaTurnos>
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    protected override ICommandHandlerAsync<QuitarJornadaDePlantillaTurnos> Handler =>
        new QuitarJornadaDePlantillaTurnosCommandHandler(EventStore);

    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaDeTurnosCreada PlantillaCreada() =>
        PlantillaDeTurnosCreada.Crear(GuidAggregateId, "Ciclo Cocina", 8);

    private JornadaDePlantillaDeTurnosAsignada JornadaAsignada() =>
        JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(), 1);

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_EmiteJornadaQuitada_CuandoLaPlantillaTieneJornada()
    {
        Given(PlantillaCreada(), EventoSinJornada(GuidAggregateId), JornadaAsignada(),
            EventoDiasSinTurno(GuidAggregateId, 8));

        await WhenAsync(new QuitarJornadaDePlantillaTurnos(GuidAggregateId));

        Then(JornadaDePlantillaDeTurnosQuitada.Crear(GuidAggregateId), EventoSinJornada(GuidAggregateId));
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaNoTieneJornada()
    {
        Given(PlantillaCreada(), EventoSinJornada(GuidAggregateId));

        await WhenAsync(new QuitarJornadaDePlantillaTurnos(GuidAggregateId));

        Then();
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(new QuitarJornadaDePlantillaTurnos(GuidAggregateId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{QuitarJornadaDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(), PlantillaDeTurnosRetirada.Crear(GuidAggregateId));

        var act = async () => await WhenAsync(new QuitarJornadaDePlantillaTurnos(GuidAggregateId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{QuitarJornadaDePlantillaTurnosCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }
}
