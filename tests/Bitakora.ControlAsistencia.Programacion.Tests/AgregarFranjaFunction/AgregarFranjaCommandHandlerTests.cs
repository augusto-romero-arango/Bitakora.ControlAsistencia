// Issue #602: agregar una franja ordinaria a un turno existente -- primer paso del diseno de
// turno por pasos (CA-ADR-0033).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarFranjaFunction;

public class AgregarFranjaCommandHandlerTests : CommandHandlerAsyncTest<AgregarFranja>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000602");

    protected override ICommandHandlerAsync<AgregarFranja> Handler =>
        new AgregarFranjaCommandHandler(EventStore);

    private static TurnoCreado CrearEventoTurnoIncompleto() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana", []);

    private static TurnoCreado CrearEventoTurnoConFranja() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    // CA-6: camino feliz -- la sede prearmada viaja en el comando y queda en la franja persistida;
    // el turno pasa de incompleto a completo con la primera franja.
    [Fact]
    public async Task AgregarFranja_EmiteFranjaAgregada_CuandoElTurnoEstaIncompleto()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoIncompleto());
        var sede = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");

        await WhenAsync(new AgregarFranja(
            TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0), Sede: sede));

        Then(TurnoId.ToString(), FranjaAgregada.Crear(
            TurnoId, FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0), sede: sede)));
        And<CatalogoTurnos, bool>(TurnoId.ToString(), c => c.EstaCompleto(), true);
        And<CatalogoTurnos, SedeProgramada?>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Sede, sede);
    }

    // CA-5: turno inexistente -> 404. Sin And<>: el aggregate no existe (ningun Given), y
    // reconstruirlo lanzaria ArgumentNullException -- mismo criterio que
    // RetirarTurnoCommandHandlerTests para el mismo escenario.
    [Fact]
    public async Task AgregarFranja_LanzaKeyNotFoundException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0)));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AgregarFranjaCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }

    // CA-5/CA-4: retirado gana la precedencia sobre solape/descanso.
    [Fact]
    public async Task AgregarFranja_LanzaInvalidOperationException_CuandoElTurnoFueRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranja(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AgregarFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }

    [Fact]
    public async Task AgregarFranja_LanzaInvalidOperationException_CuandoElTurnoEsDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AgregarFranjaCommandHandler.Mensajes.TurnoEsDescanso}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 0);
    }

    [Fact]
    public async Task AgregarFranja_LanzaInvalidOperationException_CuandoLaFranjaSeSolapaConOtraExistente()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranja());

        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(10, 0), new TimeOnly(12, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AgregarFranjaCommandHandler.Mensajes.FranjaSeSolapa}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    // Invariante del VO (duracion no positiva): el handler construye FranjaOrdinaria ANTES de leer
    // el aggregate -- la excepcion sube sin tocar el catalogo (dos canales de error, nunca
    // mezclados, CA-ADR-0030).
    [Fact]
    public async Task AgregarFranja_DejaSubirArgumentException_CuandoInicioYFinSonIguales()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranja());

        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(10, 0), new TimeOnly(10, 0)));

        await act.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.DuracionNoPositiva}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    // Invariante del VO (sede incompleta): mismo canal que la duracion -- el handler nunca llega
    // al aggregate.
    [Fact]
    public async Task AgregarFranja_DejaSubirArgumentException_CuandoLaSedeEsIncompleta()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranja());

        var act = async () => await WhenAsync(new AgregarFranja(
            TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0),
            Sede: new SedeProgramada("", "Suba")));

        await act.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage($"*{FranjaOrdinaria.Mensajes.SedeIncompleta}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }
}
