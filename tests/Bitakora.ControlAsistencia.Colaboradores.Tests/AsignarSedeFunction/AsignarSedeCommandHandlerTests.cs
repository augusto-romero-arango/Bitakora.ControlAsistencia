// Issue #465: asignar (o reasignar) la sede del colaborador -- reemplazo completo de un VO atomico
// direccionable (MEF-ADR-0043 paso 2), mismo mecanismo combinado que AsignarEtiqueta (#355):
// "declinar con resultado" para la regla de apertura estricta (VinculacionTerminada) y "declinar en
// silencio" para la idempotencia (SinCambios, comparacion EXACTA case-sensitive del codigo).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC-79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que AsignarEtiquetaCommandHandlerTests).
public class AsignarSedeCommandHandlerTests : CommandHandlerAsyncTest<AsignarSede>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
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

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion) y SIN
    // sede asignada -- base de CA-1.
    private void DadoUnColaboradorConVinculacionAbiertaSinSede() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // Precondicion (CA-2/CA-3): el colaborador ya tiene una sede asignada sobre la vinculacion vigente.
    private void DadoUnColaboradorConSedeAsignada(string codigoSede) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new SedeAsignada(codigoSede));

    // Precondicion (CA-4): la vinculacion vigente ya tiene una terminacion registrada -- incluye un
    // preaviso con fecha futura, que bloquea igual sin distincion de estado.
    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    // CA-1: vinculacion vigente sin sede -> el stream recibe SedeAsignada con el codigo del
    // comando; el aggregate rehidratado refleja la sede.
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoElColaboradorNoTieneSede()
    {
        DadoUnColaboradorConVinculacionAbiertaSinSede();

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new SedeAsignada(CodigoSedeBogota));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // CA-2: reasignar una sede DISTINTA sobre un colaborador que ya tiene sede -> reemplazo puro,
    // el mismo evento SedeAsignada con el codigo nuevo (sin evento de retiro, decision de
    // refinamiento).
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoReasignaUnaSedeDistintaALaVigente()
    {
        DadoUnColaboradorConSedeAsignada(CodigoSedeBogota);

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeMedellin });

        Then(StreamIdEsperado, new SedeAsignada(CodigoSedeMedellin));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeMedellin);
    }

    // CA-3: el codigo del comando es IGUAL (comparacion exacta, case-sensitive) a la sede ya
    // asignada -> idempotencia silenciosa: ningun evento nuevo, el estado conserva la sede original.
    [Fact]
    public async Task AsignarSede_NoEmiteEvento_CuandoElCodigoEsIgualAlVigente()
    {
        DadoUnColaboradorConSedeAsignada(CodigoSedeBogota);

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeBogota });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeBogota);
    }

    // CA-3 (comparacion case-sensitive, precedente #387): el mismo codigo con distinto case NO es
    // el mismo valor -> emite SedeAsignada (reemplazo), no idempotencia.
    [Fact]
    public async Task AsignarSede_EmiteSedeAsignada_CuandoElCodigoDifiereSoloEnMayusculas()
    {
        DadoUnColaboradorConSedeAsignada(CodigoSedeBogota);

        await WhenAsync(ComandoValido() with { CodigoSede = "bog" });

        Then(StreamIdEsperado, new SedeAsignada("bog"));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, "bog");
    }

    // CA-4: la ULTIMA vinculacion tiene terminacion registrada -> 409, ningun evento nuevo, la sede
    // (ausente en este escenario) queda intacta.
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

    // CA-4 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual -- la
    // sede describe la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
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

    // CA-4 x CA-3 (cruce, mismo criterio que AsignarEtiqueta #355): la vinculacion tiene
    // terminacion registrada Y el codigo del comando es identico al ya asignado. La regla de
    // apertura es incondicional -- gana sobre la idempotencia silenciosa.
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

    // CA-5 (reingreso nace sin sede): tras un reingreso, la vinculacion nueva no hereda la sede de
    // la anterior -- si el aggregate NO limpiara _codigoSede en Apply(VinculacionIniciada), asignar
    // el MISMO codigo que tenia la vinculacion anterior seria SinCambios (ningun evento); como la
    // sede nace limpia, el comando emite SedeAsignada de todos modos.
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

    // CA-6: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe.
    [Fact]
    public async Task AsignarSede_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarSedeCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
