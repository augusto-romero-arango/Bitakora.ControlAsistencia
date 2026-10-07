using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarJornadaDePlantillaSemanalFunction;

public class QuitarJornadaDePlantillaSemanalCommandHandlerTests
    : CommandHandlerAsyncTest<QuitarJornadaDePlantillaSemanal>
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    protected override ICommandHandlerAsync<QuitarJornadaDePlantillaSemanal> Handler =>
        new QuitarJornadaDePlantillaSemanalCommandHandler(EventStore);

    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaSemanalCreada PlantillaCreada() =>
        PlantillaSemanalCreada.Crear(GuidAggregateId, "Semana Cocina", 2);

    private JornadaDePlantillaSemanalAsignada JornadaAsignada() =>
        JornadaDePlantillaSemanalAsignada.Crear(GuidAggregateId, JornadaId, Limites(), 1);

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_EmiteJornadaQuitada_CuandoLaPlantillaTieneJornada()
    {
        Given(PlantillaCreada(), JornadaAsignada());

        await WhenAsync(new QuitarJornadaDePlantillaSemanal(GuidAggregateId));

        Then(JornadaDePlantillaSemanalQuitada.Crear(GuidAggregateId), AdvertenciasDePlantillaSemanalCalculadas.Crear(GuidAggregateId, [AdvertenciaPlantillaSemanal.PlantillaSinJornada()]));
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_NoEmiteEventos_CuandoLaPlantillaNoTieneJornada()
    {
        Given(PlantillaCreada());

        await WhenAsync(new QuitarJornadaDePlantillaSemanal(GuidAggregateId));

        Then();
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, null);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(new QuitarJornadaDePlantillaSemanal(GuidAggregateId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{QuitarJornadaDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(), PlantillaSemanalRetirada.Crear(GuidAggregateId));

        var act = async () => await WhenAsync(new QuitarJornadaDePlantillaSemanal(GuidAggregateId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{QuitarJornadaDePlantillaSemanalCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }
}
