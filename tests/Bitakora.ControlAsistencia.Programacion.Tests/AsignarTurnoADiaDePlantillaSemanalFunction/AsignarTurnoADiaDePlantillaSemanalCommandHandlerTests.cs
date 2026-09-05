// El comando toca DOS streams: la plantilla (bajo GuidAggregateId) y el turno del catalogo (bajo
// su propio TurnoId, pre-cargado con el overload Given(streamId, evento)) -- mismo patron que
// SolicitarProgramacionTurnoCommandHandlerTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
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

        Then(DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_EmiteDiaAsignado_CuandoElTurnoEsDescanso()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoDescanso());

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaKeyNotFoundException_CuandoLaPlantillaNoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(TurnoId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaKeyNotFoundException_CuandoElTurnoNoExiste()
    {
        Given(CrearEventoPlantilla());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaInvalidOperationException_CuandoElTurnoEstaRetirado()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.TurnoRetirado}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaInvalidOperationException_CuandoElTurnoEstaIncompleto()
    {
        Given(CrearEventoPlantilla());
        Given(TurnoId.ToString(), CrearEventoTurnoIncompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.TurnoIncompleto}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_LanzaInvalidOperationException_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        Given(CrearEventoPlantilla(semanas: 2));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        var act = async () => await WhenAsync(
            new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 3, DiaSemana.Desde(5), TurnoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarTurnoADiaDePlantillaSemanalCommandHandler.Mensajes.SemanaFueraDeRango}*");
        Then(GuidAggregateId.ToString());
    }

    // Idempotencia (ResultadoAsignarDia.SinCambios): los dos Then sin eventos esperados afirman
    // que no se emitio nada en NINGUNO de los dos streams.
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_NoEmiteEvento_CuandoElMismoTurnoYaEstaAsignadoAEseDia()
    {
        Given(CrearEventoPlantilla(),
            DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));
        Given(TurnoId.ToString(), CrearEventoTurnoCompleto());

        await WhenAsync(new AsignarTurnoADiaDePlantillaSemanal(GuidAggregateId, 1, DiaSemana.Desde(5), TurnoId));

        Then(GuidAggregateId.ToString());
        Then(TurnoId.ToString());
    }
}
