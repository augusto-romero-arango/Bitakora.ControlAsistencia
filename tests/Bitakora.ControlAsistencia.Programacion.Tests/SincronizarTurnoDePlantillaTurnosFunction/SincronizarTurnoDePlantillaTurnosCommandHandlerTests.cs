using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarTurnoDePlantillaTurnosFunction;

public class SincronizarTurnoDePlantillaTurnosCommandHandlerTests
    : CommandHandlerAsyncTest<SincronizarTurnoDePlantillaTurnos>
{
    private const int DiasDelCiclo = 4;
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001285");
    private static readonly Guid OtroTurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001286");

    protected override ICommandHandlerAsync<SincronizarTurnoDePlantillaTurnos> Handler =>
        new SincronizarTurnoDePlantillaTurnosCommandHandler(EventStore);

    private static readonly Turno TurnoDeOchoHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno TurnoDeDiezHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0))]);

    private static readonly Turno TurnoOtro = Turno.Crear("Turno Tarde", false,
        [FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0))]);

    private PlantillaDeTurnosCreada PlantillaCreada() =>
        PlantillaDeTurnosCreada.Crear(GuidAggregateId, "Ciclo Cocina", DiasDelCiclo);

    private DiaDePlantillaDeTurnosAsignado Dia(int dia, Guid turnoId, Turno turno, long version) =>
        DiaDePlantillaDeTurnosAsignado.Crear(GuidAggregateId, dia, turnoId, turno, version);

    private void GivenPlantillaConTurnoEnDosDias(long version = 1) =>
        Given(PlantillaCreada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, version),
            Dia(2, TurnoId, TurnoDeOchoHoras, version));

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_EmiteSincronizado_CuandoLaVersionEsMayor()
    {
        GivenPlantillaConTurnoEnDosDias();

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        Then(TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 2);
        And<PlantillaTurnos, bool>(p => p.DiaEstaRetirado(1), false);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoToca_LosDiasDeOtroTurno()
    {
        Given(PlantillaCreada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            Dia(2, OtroTurnoId, TurnoOtro, 1));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        Then(TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(OtroTurnoId), 1);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionEsIgualALaVigente()
    {
        GivenPlantillaConTurnoEnDosDias(version: 3);

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 3, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 3);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionEsMenorALaVigente()
    {
        GivenPlantillaConTurnoEnDosDias(version: 5);

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 4, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 5);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoNingunDiaUsaElTurno()
    {
        GivenPlantillaConTurnoEnDosDias();

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, OtroTurnoId, TurnoOtro, 9, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(OtroTurnoId), 0);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_MarcaLosDiasComoRetirados_CuandoElTurnoVieneRetirado()
    {
        Given(PlantillaCreada(), Dia(1, TurnoId, TurnoDeOchoHoras, 1));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));

        Then(TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));
        And<PlantillaTurnos, bool>(p => p.DiaEstaRetirado(1), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionYaFueSincronizadaComoRetirada()
    {
        Given(PlantillaCreada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));

        Then();
        And<PlantillaTurnos, bool>(p => p.DiaEstaRetirado(1), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            PlantillaDeTurnosRetirada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 1);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(
            new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SincronizarTurnoDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }
}
