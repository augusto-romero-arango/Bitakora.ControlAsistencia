// Issue #606: cambiar (o retirar) la sede prearmada de una franja ya creada -- cuarto paso del
// diseno de turno por pasos (CA-ADR-0033).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarSedeAFranjaFunction;

public class AsignarSedeAFranjaCommandHandlerTests : CommandHandlerAsyncTest<AsignarSedeAFranja>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000606");
    private static readonly SedeProgramada Chapinero = new("SEDE-CHAPINERO", "Chapinero");

    protected override ICommandHandlerAsync<AsignarSedeAFranja> Handler =>
        new AsignarSedeAFranjaCommandHandler(EventStore);

    private static TurnoCreado CrearEventoTurnoConFranjaSinSede() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(14, 0), new TimeOnly(22, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoConFranjaConSede() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(14, 0), new TimeOnly(22, 0), [], [], Chapinero)]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    // CA-4: camino feliz -- la franja sin sede recibe la sede prearmada indicada.
    [Fact]
    public async Task AsignarSedeAFranja_EmiteSedeDeFranjaAsignada_CuandoLaFranjaNoTeniaSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaSinSede());

        await WhenAsync(new AsignarSedeAFranja(TurnoId, new TimeOnly(14, 0), Chapinero));

        var franjaEsperada = FranjaOrdinaria.Crear(
            new TimeOnly(14, 0), new TimeOnly(22, 0), sede: Chapinero);
        Then(TurnoId.ToString(), SedeDeFranjaAsignada.Crear(TurnoId, franjaEsperada));
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, Chapinero);
    }

    // CA-4: retirar -- Sede=null sobre una franja con sede emite SedeDeFranjaRetirada.
    [Fact]
    public async Task AsignarSedeAFranja_EmiteSedeDeFranjaRetirada_CuandoLaSedeEsNullYLaFranjaTeniaSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaConSede());

        await WhenAsync(new AsignarSedeAFranja(TurnoId, new TimeOnly(14, 0), null));

        var franjaEsperada = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));
        Then(TurnoId.ToString(), SedeDeFranjaRetirada.Crear(TurnoId, franjaEsperada));
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, null);
    }

    // CA-4: turno inexistente -> 404. Sin And<>: el aggregate no existe (ningun Given), y
    // reconstruirlo lanzaria ArgumentNullException -- mismo criterio que
    // AgregarFranjaCommandHandlerTests para el mismo escenario.
    [Fact]
    public async Task AsignarSedeAFranja_LanzaKeyNotFoundException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(
            new AsignarSedeAFranja(TurnoId, new TimeOnly(14, 0), Chapinero));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarSedeAFranjaCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }

    [Fact]
    public async Task AsignarSedeAFranja_LanzaInvalidOperationException_CuandoElTurnoFueRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaSinSede(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(
            new AsignarSedeAFranja(TurnoId, new TimeOnly(14, 0), Chapinero));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeAFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, null);
    }

    [Fact]
    public async Task AsignarSedeAFranja_LanzaInvalidOperationException_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaSinSede());

        var act = async () => await WhenAsync(
            new AsignarSedeAFranja(TurnoId, new TimeOnly(15, 0), Chapinero));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeAFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    // Un descanso no tiene franjas ordinarias -- misma razon que QuitarFranja/QuitarDescanso.
    [Fact]
    public async Task AsignarSedeAFranja_LanzaInvalidOperationException_CuandoElTurnoEsDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        var act = async () => await WhenAsync(
            new AsignarSedeAFranja(TurnoId, new TimeOnly(14, 0), Chapinero));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeAFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 0);
    }

    // CA-3: retirar dos veces -- nada que retirar, sin evento (mismo criterio que
    // ResultadoRetiroTurno.YaEstabaRetirado).
    [Fact]
    public async Task AsignarSedeAFranja_LanzaInvalidOperationException_CuandoLaFranjaYaNoTieneSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaSinSede());

        var act = async () => await WhenAsync(
            new AsignarSedeAFranja(TurnoId, new TimeOnly(14, 0), null));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeAFranjaCommandHandler.Mensajes.FranjaSinSede}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, null);
    }

    // Invariante del VO (sede incompleta): el handler construye la franja resultante via ConSede
    // ANTES de tocar el catalogo -- misma separacion de canales que AgregarFranjaCommandHandler
    // (CA-ADR-0030).
    [Fact]
    public async Task AsignarSedeAFranja_DejaSubirArgumentException_CuandoLaSedeEsIncompleta()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaSinSede());

        var act = async () => await WhenAsync(new AsignarSedeAFranja(
            TurnoId, new TimeOnly(14, 0), new SedeProgramada("", "Suba")));

        await act.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.SedeIncompleta}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, null);
    }
}
