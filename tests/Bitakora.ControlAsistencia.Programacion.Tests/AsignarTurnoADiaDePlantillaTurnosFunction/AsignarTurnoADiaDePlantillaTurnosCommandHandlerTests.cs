using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using static Bitakora.ControlAsistencia.Programacion.Tests.Entities.AdvertenciasEsperadasPlantillaTurnos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaTurnosFunction;

public class AsignarTurnoADiaDePlantillaTurnosCommandHandlerTests
    : CommandHandlerAsyncTest<AsignarTurnoADiaDePlantillaTurnos>
{
    private const string NombrePlantilla = "Ciclo 8 dias";
    private const int DiasDelCiclo = 8;
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001281");

    protected override ICommandHandlerAsync<AsignarTurnoADiaDePlantillaTurnos> Handler =>
        new AsignarTurnoADiaDePlantillaTurnosCommandHandler(EventStore);

    // El TestStore no puebla AggregateRoot.Version: la version del stream del turno es 0.
    private const long VersionEnElHarness = 0;

    private static readonly Turno CopiaTurnoCompleto =
        Turno.Crear("Turno Manana", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno CopiaTurnoDescanso = Turno.Crear("Descanso Compensatorio", true, []);

    private PlantillaDeTurnosCreada CrearEventoPlantilla() =>
        PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, DiasDelCiclo);

    private static TurnoCreado CrearEventoTurnoCompleto() =>
        TurnoCreado.Crear(
            TurnoId, "Turno Manana", [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    private static TurnoCreado CrearEventoTurnoIncompleto() =>
        TurnoCreado.Crear(TurnoId, "Turno Incompleto", []);

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_EmiteDiaAsignado_CuandoElTurnoEstaCompleto()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        Then(DiaDePlantillaDeTurnosAsignado.Crear(
            GuidAggregateId, 3, TurnoId, CopiaTurnoCompleto, VersionEnElHarness));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_EmiteDiaAsignado_CuandoElTurnoEsDescanso()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoDescanso());

        await WhenAsync(new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        Then(DiaDePlantillaDeTurnosAsignado.Crear(
            GuidAggregateId, 3, TurnoId, CopiaTurnoDescanso, VersionEnElHarness));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_EmiteDiaAsignado_CuandoElDiaTieneOtroTurno()
    {
        var otroTurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001282");
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId),
            DiaDePlantillaDeTurnosAsignado.Crear(
                GuidAggregateId, 3, otroTurnoId, CopiaTurnoDescanso, VersionEnElHarness));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        Then(DiaDePlantillaDeTurnosAsignado.Crear(
            GuidAggregateId, 3, TurnoId, CopiaTurnoCompleto, VersionEnElHarness));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_EmiteDiaAsignado_CuandoElDiaEsElUltimoDelCiclo()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, DiasDelCiclo, TurnoId));

        Then(DiaDePlantillaDeTurnosAsignado.Crear(
            GuidAggregateId, DiasDelCiclo, TurnoId, CopiaTurnoCompleto, VersionEnElHarness));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_NoEmiteEvento_CuandoElMismoTurnoYaEstaAsignadoAEseDia()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId),
            DiaDePlantillaDeTurnosAsignado.Crear(
                GuidAggregateId, 3, TurnoId, CopiaTurnoCompleto, VersionEnElHarness));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        Then(GuidAggregateId.ToString());
        Then(TurnoId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, string>(TurnoId.ToString(), c => c.Id, TurnoId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoElTurnoNoExiste()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEstaRetirado()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.TurnoRetirado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEstaIncompleto()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoIncompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.TurnoIncompleto}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoElDiaSuperaElCiclo()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, DiasDelCiclo + 1, TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.DiaFueraDelCiclo}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaReglaDeNegocioDeclinada_CuandoLaPlantillaEstaRetirada()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId), PlantillaDeTurnosRetirada.Crear(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, 3, TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_LanzaReglaDeNegocioDeclinadaPorRetiro_CuandoLaPlantillaEstaRetiradaYElDiaFueraDelCiclo()
    {
        Given(CrearEventoPlantilla(), EventoSinJornada(GuidAggregateId), PlantillaDeTurnosRetirada.Crear(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaTurnos(GuidAggregateId, DiasDelCiclo + 1, TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }
}
