// Issue #349: terminar la vinculacion de un colaborador -- segundo comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357). CA-ADR-0030: el aggregate declina con resultado
// (nunca lanza, nunca emite evento de fallo); el handler traduce la razon a InvalidOperationException
// (409) o KeyNotFoundException (404).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del test-writer,
// mismo criterio que RegistrarColaboradorCommandHandlerTests).
public class TerminarVinculacionCommandHandlerTests : CommandHandlerAsyncTest<TerminarVinculacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId (mismo criterio que #330).
    private const string StreamIdEsperado = "CC:79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaValida = new(2026, 6, 1);

    protected override ICommandHandlerAsync<TerminarVinculacion> Handler =>
        new TerminarVinculacionCommandHandler(EventStore);

    private static TerminarVinculacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        FechaEfectiva: FechaEfectivaValida);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    // Precondicion compartida: colaborador registrado con una vinculacion abierta (sin terminacion).
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // CA-1: colaborador con vinculacion abierta + comando valido -> el stream recibe
    // VinculacionTerminada con la FechaEfectiva del request; el aggregate rehidratado refleja la
    // terminacion. (El registro en IdentidadEventosColaboradores.TiposPersistidos lo cubren
    // AliasEventosColaboradoresTests/ComposicionServiciosTests, no este handler.)
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoLaVinculacionEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionTerminada(FechaEfectivaValida));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaValida);
    }

    // CA-2: FechaEfectiva futura (preaviso) -> aceptada igual que una pasada -- ninguna validacion
    // contra el reloj del servidor en ninguna direccion.
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoFechaEfectivaEsFutura()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaFutura = new DateOnly(2030, 1, 1);

        await WhenAsync(ComandoValido() with { FechaEfectiva = fechaFutura });

        Then(StreamIdEsperado, new VinculacionTerminada(fechaFutura));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaFutura);
    }

    // CA-2 (segunda direccion): FechaEfectiva pasada (registro tardio, posterior al inicio de la
    // vinculacion pero anterior a "hoy") -> tambien aceptada, sin validacion contra el reloj.
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoFechaEfectivaEsPasada()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaPasada = FechaInicioVinculacionVigente.AddDays(5);

        await WhenAsync(ComandoValido() with { FechaEfectiva = fechaPasada });

        Then(StreamIdEsperado, new VinculacionTerminada(fechaPasada));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaPasada);
    }

    // CA-3: la ultima vinculacion ya tiene terminacion registrada -> 409, ningun evento nuevo en
    // el stream, y el estado conserva la terminacion previa (no la del comando rechazado).
    [Fact]
    public async Task TerminarVinculacion_LanzaInvalidOperationException_CuandoLaVinculacionYaTieneTerminacionRegistrada()
    {
        var fechaTerminacionPrevia = new DateOnly(2026, 3, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionPrevia));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaTerminacionPrevia);
    }

    // CA-3 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual una
    // segunda terminacion -- "ya terminada" se evalua solo con la historia del stream, sin reloj.
    [Fact]
    public async Task TerminarVinculacion_LanzaInvalidOperationException_CuandoYaExisteUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaPreavisoFutura));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaPreavisoFutura);
    }

    // CA-4: FechaEfectiva anterior a FechaInicio -> 409 (duracion negativa); el estado no cambia
    // (la vinculacion sigue abierta, sin terminacion).
    [Fact]
    public async Task TerminarVinculacion_LanzaInvalidOperationException_CuandoFechaEfectivaEsAnteriorAFechaInicio()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaAnterior = FechaInicioVinculacionVigente.AddDays(-1);

        var act = async () => await WhenAsync(ComandoValido() with { FechaEfectiva = fechaAnterior });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.FechaAnteriorAInicio}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-4 (limite): FechaEfectiva == FechaInicio -> exito (vinculacion de un solo dia).
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoFechaEfectivaEsIgualAFechaInicio()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoValido() with { FechaEfectiva = FechaInicioVinculacionVigente });

        Then(StreamIdEsperado, new VinculacionTerminada(FechaInicioVinculacionVigente));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaInicioVinculacionVigente);
    }

    // Cierre del ciclo completo (issue #350): la vinculacion vigente nacio de un reingreso, no del
    // registro. Terminarla vuelve a ser posible porque Apply(VinculacionIniciada) reabre la
    // vinculacion al re-aplicarse -- sin ese reset, este comando chocaria contra la terminacion
    // heredada de la vinculacion anterior y responderia 409.
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoLaVinculacionVigenteNacioDeUnReingreso()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada("COL-002", fechaInicioReingreso));

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionTerminada(FechaEfectivaValida));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaValida);
    }

    // Cierre del ciclo completo (issue #350), segunda direccion: la ventana de fechas validas se
    // mueve con el reingreso. Una FechaEfectiva posterior al inicio ORIGINAL pero anterior al inicio
    // del REINGRESO produce duracion negativa sobre la vinculacion vigente -> 409.
    [Fact]
    public async Task TerminarVinculacion_LanzaInvalidOperationException_CuandoFechaEfectivaEsAnteriorAlInicioDelReingreso()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada("COL-002", fechaInicioReingreso));

        var act = async () => await WhenAsync(
            ComandoValido() with { FechaEfectiva = fechaInicioReingreso.AddDays(-1) });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.FechaAnteriorAInicio}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-5: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Sin And<>: el aggregate no existe en el TestStore
    // (GetAggregateRoot retorna null), invocarlo lanzaria ArgumentNullException -- Then sin
    // eventos esperados ya demuestra "sin escribir nada al event store".
    [Fact]
    public async Task TerminarVinculacion_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
