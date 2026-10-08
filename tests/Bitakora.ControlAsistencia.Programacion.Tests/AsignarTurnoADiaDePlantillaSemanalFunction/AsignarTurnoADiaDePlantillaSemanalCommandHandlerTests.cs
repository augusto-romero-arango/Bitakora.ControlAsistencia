// El comando toca DOS streams: la plantilla (bajo GuidAggregateId) y el turno del catalogo (bajo
// su propio TurnoId, pre-cargado con el overload Given(streamId, evento)) -- mismo patron que
// SolicitarProgramacionTurnoCommandHandlerTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Tests.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction;

public class AsignarTurnoADiaDePlantillaSemanalCommandHandlerTests
    : CommandHandlerAsyncTest<AsignarTurnoADiaDePlantillaSemanal>
{
    private const string NombrePlantilla = "Semana Cocina";
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    protected override ICommandHandlerAsync<AsignarTurnoADiaDePlantillaSemanal> Handler =>
        new AsignarTurnoADiaDePlantillaSemanalCommandHandler(EventStore);

    // El TestStore reconstruye los aggregates aplicando eventos y no puebla AggregateRoot.Version:
    // la version del stream del turno en el harness es siempre 0.
    private const long VersionEnElHarness = 0;

    private static readonly Turno CopiaTurnoCompleto =
        Turno.Crear("Turno Manana", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno CopiaTurnoDescanso = Turno.Crear("Descanso Compensatorio", true, []);

    private PlantillaSemanalCreada CrearEventoPlantilla(int semanas = 2) =>
        PlantillaSemanalCreada.Crear(GuidAggregateId, NombrePlantilla, semanas);

    private static TurnoCreado CrearEventoTurnoCompleto() =>
        TurnoCreado.Crear(
            TurnoId, "Turno Manana", [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    // Un descanso es completo aunque tenga cero franjas ordinarias (CA-ADR-0033).
    private static TurnoCreado CrearEventoTurnoDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    // Turno incompleto: nace vacio y sin marca de descanso (CA-ADR-0033).
    private static TurnoCreado CrearEventoTurnoIncompleto() =>
        TurnoCreado.Crear(TurnoId, "Turno Incompleto", []);

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_EmiteDiaAsignado_CuandoElTurnoEstaCompleto()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(DiaDePlantillaSemanalAsignado.Crear(
            GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId, CopiaTurnoCompleto, VersionEnElHarness),
            AdvertenciasEsperadasPlantilla.SinJornada(GuidAggregateId));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_EmiteDiaAsignado_CuandoElTurnoEsDescanso()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoDescanso());

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(DiaDePlantillaSemanalAsignado.Crear(
            GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId, CopiaTurnoDescanso, VersionEnElHarness),
            AdvertenciasEsperadasPlantilla.SinJornada(GuidAggregateId));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(TurnoId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoElTurnoNoExiste()
    {
        Given(CrearEventoPlantilla());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEstaRetirado()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.TurnoRetirado}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEstaIncompleto()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoIncompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.TurnoIncompleto}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        Given(CrearEventoPlantilla(semanas: 2));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 3, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.SemanaFueraDeRango}*");
        Then(GuidAggregateId.ToString());
    }

    // CA-5 (issue #623): la plantilla retirada gana a cualquier otra evaluacion del handler.
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoLaPlantillaEstaRetirada()
    {
        Given(CrearEventoPlantilla(), PlantillaSemanalRetirada.Crear(GuidAggregateId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.PlantillaRetirada}*");
        Then(GuidAggregateId.ToString());
        Then(TurnoId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // Idempotencia (ResultadoAsignarDia.SinCambios): los dos Then sin eventos esperados afirman
    // que no se emitio nada en NINGUNO de los dos streams.
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_NoEmiteEvento_CuandoElMismoTurnoYaEstaAsignadoAEseDia()
    {
        Given(CrearEventoPlantilla(),
            DiaDePlantillaSemanalAsignado.Crear(
                GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId, CopiaTurnoCompleto, VersionEnElHarness));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(GuidAggregateId.ToString());
        Then(TurnoId.ToString());
    }

    // CA-1: la copia es la del catalogo en ese momento, incluidas las ediciones posteriores a la creacion.
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_EmiteDiaAsignadoConLaCopiaActual_CuandoElTurnoFueEditado()
    {
        var franjaTarde = FranjaOrdinaria.Crear(new TimeOnly(15, 0), new TimeOnly(18, 0));
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto(), FranjaAgregada.Crear(TurnoId, franjaTarde));
        var copiaEsperada = Turno.Crear("Turno Manana", false,
            [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0)), franjaTarde]);

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(DiaDePlantillaSemanalAsignado.Crear(
            GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId, copiaEsperada, VersionEnElHarness),
            AdvertenciasEsperadasPlantilla.SinJornada(GuidAggregateId));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-1: asignar sobre un dia ocupado por otro turno reemplaza el slot con la copia del nuevo.
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_EmiteDiaAsignado_CuandoElDiaTieneOtroTurno()
    {
        var otroTurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000702");
        Given(CrearEventoPlantilla(),
            DiaDePlantillaSemanalAsignado.Crear(
                GuidAggregateId, 1, DiaSemana.Desde(5), otroTurnoId, CopiaTurnoDescanso, VersionEnElHarness));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(DiaDePlantillaSemanalAsignado.Crear(
            GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId, CopiaTurnoCompleto, VersionEnElHarness),
            AdvertenciasEsperadasPlantilla.SinJornada(GuidAggregateId));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }
}
