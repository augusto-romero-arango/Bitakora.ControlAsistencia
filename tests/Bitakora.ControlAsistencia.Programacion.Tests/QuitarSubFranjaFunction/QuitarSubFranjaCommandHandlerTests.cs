// Issue #605: quitar un descanso o un extra de una franja de un turno -- espejo de #603
// (AgregarSubFranja): mismo discriminador de frontera Tipo, dos eventos gemelos, la franja
// contenedora se reemplaza por una nueva sin la hija.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarSubFranjaFunction;

public class QuitarSubFranjaCommandHandlerTests : CommandHandlerAsyncTest<QuitarSubFranja>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000605");

    protected override ICommandHandlerAsync<QuitarSubFranja> Handler =>
        new QuitarSubFranjaCommandHandler(EventStore);

    private static TurnoCreado CrearEventoTurnoConFranjaNocturna() =>
        TurnoCreado.Crear(TurnoId, "Turno Manana",
            [new DatosFranja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], [])]);

    private static TurnoCreado CrearEventoTurnoDeDescanso() =>
        TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

    private static DescansoAgregado CrearEventoDescansoAgregado() =>
        DescansoAgregado.Crear(TurnoId,
            FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
                .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30)));

    // CA-4: camino feliz -- Tipo = Descanso emite DescansoQuitado con la franja resultante,
    // ya sin la hija.
    [Fact]
    public async Task QuitarSubFranja_EmiteDescansoQuitado_CuandoElDescansoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna(), CrearEventoDescansoAgregado());

        await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso, new TimeOnly(2, 0)));

        Then(TurnoId.ToString(), DescansoQuitado.Crear(
            TurnoId, FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))));
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 0);
    }

    // CA-4: Tipo = Extra emite ExtraQuitado.
    [Fact]
    public async Task QuitarSubFranja_EmiteExtraQuitado_CuandoElExtraExiste()
    {
        var extraAgregado = ExtraAgregado.Crear(TurnoId,
            FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))
                .ConExtra(new TimeOnly(5, 0), new TimeOnly(6, 0)));
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna(), extraAgregado);

        await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Extra, new TimeOnly(5, 0)));

        Then(TurnoId.ToString(), ExtraQuitado.Crear(
            TurnoId, FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0))));
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Extras.Count, 0);
    }

    // Sin And<>: el aggregate no existe (ningun Given) y reconstruirlo lanzaria
    // ArgumentNullException -- mismo criterio que AgregarSubFranjaCommandHandlerTests.
    [Fact]
    public async Task QuitarSubFranja_LanzaKeyNotFoundException_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso, new TimeOnly(2, 0)));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.TurnoNoEncontrado}*");
        Then(TurnoId.ToString());
    }

    // Precedencia: retirado gana sobre franja-no-existe y sobre subfranja-no-existe.
    [Fact]
    public async Task QuitarSubFranja_LanzaInvalidOperationException_CuandoElTurnoFueRetirado()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna(), CrearEventoDescansoAgregado(),
            TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso, new TimeOnly(2, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 1);
    }

    [Fact]
    public async Task QuitarSubFranja_LanzaInvalidOperationException_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna());

        var act = async () => await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(23, 0), TipoSubFranja.Descanso, new TimeOnly(2, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 0);
    }

    // Un descanso no tiene franjas ordinarias: cae en franja-no-existe, sin resultado propio
    // (mismo criterio que QuitarFranjaCommandHandler, #604).
    [Fact]
    public async Task QuitarSubFranja_LanzaInvalidOperationException_CuandoElTurnoEsDescanso()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoDeDescanso());

        var act = async () => await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Descanso, new TimeOnly(2, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.FranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, bool>(TurnoId.ToString(), c => c.EstaCompleto(), true);
    }

    // CA-3: la franja existe, pero ninguna hija de ese tipo empieza a esa hora.
    [Fact]
    public async Task QuitarSubFranja_LanzaInvalidOperationException_CuandoLaSubFranjaNoExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurnoConFranjaNocturna(), CrearEventoDescansoAgregado());

        var act = async () => await WhenAsync(new QuitarSubFranja(
            TurnoId, new TimeOnly(22, 0), TipoSubFranja.Extra, new TimeOnly(2, 0)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.SubFranjaNoExiste}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias[0].Descansos.Count, 1);
    }
}
