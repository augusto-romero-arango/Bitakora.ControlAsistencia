using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

// El aggregate usa stream ID de texto (Identificacion.ToString()), no el GuidAggregateId del
// harness: Given/Then/And exigen los overloads que reciben el streamId explicito.
public class AsignarSedeCommandHandlerTests : CommandHandlerAsyncTest<AsignarSede>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente (MEF-ADR-0002): literal, nunca derivado de ComputarStreamId -- si se
    // derivara, un cambio de formato de la clave se auto-validaria.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private const string CodigoVinculacionReingreso = "COL-002";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingreso = new(2026, 7, 1);

    private const string CodigoSedeBogota = "BOG";
    private const string CodigoSedeMedellin = "MED";

    protected override ICommandHandlerAsync<AsignarSede> Handler =>
        new AsignarSedeCommandHandler(EventStore);

    private static AsignarSede ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        CodigoSede: CodigoSedeBogota);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    private void DadoUnColaboradorConVinculacionAbiertaSinSede() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    private void DadoUnColaboradorConSedeAsignada(string codigoSede) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new SedeAsignada(codigoSede));

    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoElColaboradorNoTieneSede()
    {
        DadoUnColaboradorConVinculacionAbiertaSinSede();

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new SedeAsignada(CodigoSedeBogota));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // Reemplazo puro: el mismo tipo de evento con el codigo nuevo, sin evento de retiro previo.
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoReasignaUnaSedeDistintaALaVigente()
    {
        DadoUnColaboradorConSedeAsignada(CodigoSedeBogota);

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeMedellin });

        Then(StreamIdEsperado, new SedeAsignada(CodigoSedeMedellin));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeMedellin);
    }

    [Fact]
    public async Task AsignarSede_NoEmiteEvento_CuandoElCodigoEsIgualAlVigente()
    {
        DadoUnColaboradorConSedeAsignada(CodigoSedeBogota);

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeBogota });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // Dos codigos que solo difieren en mayusculas son sedes distintas: no hay normalizacion.
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoElCodigoDifiereSoloEnMayusculas()
    {
        DadoUnColaboradorConSedeAsignada(CodigoSedeBogota);

        await WhenAsync(ComandoValido() with { CodigoSede = "bog" });

        Then(StreamIdEsperado, new SedeAsignada("bog"));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, "bog");
    }

    [Fact]
    public async Task AsignarSede_LanzaInvalidOperationException_CuandoLaUltimaVinculacionTieneTerminacionRegistrada()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, null);
    }

    // La terminacion bloquea aunque su fecha efectiva no haya llegado: no se consulta el reloj.
    [Fact]
    public async Task AsignarSede_LanzaInvalidOperationException_CuandoLaTerminacionEsUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConTerminacionRegistrada(fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, null);
    }

    // Cruce de las dos guardas: el rechazo por terminacion gana sobre la idempotencia silenciosa,
    // asi que el orden de las guardas en AsignarSede no es intercambiable.
    [Fact]
    public async Task AsignarSede_LanzaInvalidOperationException_CuandoElCodigoEsIgualPeroLaVinculacionTieneTerminacionRegistrada()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new SedeAsignada(CodigoSedeBogota),
            new VinculacionTerminada(FechaEfectivaTerminacion));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarSedeCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // Prueba indirecta de que Apply(VinculacionIniciada) limpia la sede: si no la limpiara, asignar
    // el mismo codigo de la vinculacion anterior seria SinCambios y no habria evento.
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoLaVinculacionEsUnReingresoTrasUnaTerminacionConSedeAsignada()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new SedeAsignada(CodigoSedeBogota),
            new VinculacionTerminada(FechaEfectivaTerminacion),
            new VinculacionIniciada(CodigoVinculacionReingreso, FechaInicioReingreso));

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeBogota });

        Then(StreamIdEsperado, new SedeAsignada(CodigoSedeBogota));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // Anular la terminacion reabre la vinculacion: la guarda lee el estado rehidratado, no un flag
    // que quede pegado tras el primer VinculacionTerminada.
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoLaTerminacionDeLaVinculacionFueAnulada()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(FechaEfectivaTerminacion),
            new TerminacionAnulada());

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new SedeAsignada(CodigoSedeBogota));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    [Fact]
    public async Task AsignarSede_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarSedeCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
