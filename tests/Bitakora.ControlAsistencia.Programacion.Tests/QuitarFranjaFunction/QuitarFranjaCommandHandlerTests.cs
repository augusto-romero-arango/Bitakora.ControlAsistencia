using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarFranjaFunction;

public class QuitarFranjaCommandHandlerTests : CommandHandlerAsyncTest<QuitarFranja>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000604");
    private static readonly SedeProgramada Sede = new("SEDE-SUBA", "Suba");

    protected override ICommandHandlerAsync<QuitarFranja> Handler =>
        new QuitarFranjaCommandHandler(EventStore);

    private static TurnoCreado CrearEventoTurnoConDosFranjas() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
        [
            new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(9, 0), new TimeOnly(9, 15))], [], Sede),
            new DatosFranja(new TimeOnly(14, 0), new TimeOnly(22, 0), [], [])
        ]);

    private static TurnoCreado CrearEventoTurnoConUnaFranja() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    private static FranjaOrdinaria FranjaEsperadaConDescansoYSede() =>
        FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0),
            descansos: [SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(9, 15))], sede: Sede);

    [Fact]
    public async Task QuitarFranja_EmiteFranjaQuitada_CuandoQuedanOtrasFranjas()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConDosFranjas());

        await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(6, 0)));

        Then(TurnoId.ToString(), FranjaQuitada.Crear(TurnoId, FranjaEsperadaConDescansoYSede()));
        And<CatalogoTurnos, bool>(TurnoId.ToString(), c => c.EstaCompleto(), true);
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    [Fact]
    public async Task QuitarFranja_EmiteFranjaQuitadaYDejaElTurnoIncompleto_CuandoEraLaUnicaFranja()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConUnaFranja());

        await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(6, 0)));

        Then(TurnoId.ToString(), FranjaQuitada.Crear(
            TurnoId, FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))));
        And<CatalogoTurnos, bool>(TurnoId.ToString(), c => c.EstaCompleto(), false);
    }

    [Fact]
    public async Task QuitarFranja_LanzaKeyNotFoundException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(6, 0)));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{QuitarFranjaCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }

    // La hora 06:00 si existe entre las franjas: lo que decide es la precedencia del retiro.
    [Fact]
    public async Task QuitarFranja_LanzaInvalidOperationException_CuandoElTurnoFueRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConDosFranjas(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(6, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 2);
    }

    [Fact]
    public async Task QuitarFranja_LanzaInvalidOperationException_CuandoLaFranjaNoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConDosFranjas());

        var act = async () => await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(7, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 2);
    }

    // Un descanso no tiene franjas: cae en "franja no existe", sin resultado propio.
    [Fact]
    public async Task QuitarFranja_LanzaInvalidOperationException_CuandoElTurnoEsDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        var act = async () => await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(6, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, bool>(TurnoId.ToString(), c => c.EstaCompleto(), true);
    }
}
