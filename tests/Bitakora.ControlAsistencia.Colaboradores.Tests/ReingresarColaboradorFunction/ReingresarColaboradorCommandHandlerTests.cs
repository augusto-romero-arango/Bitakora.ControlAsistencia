// Issue #350: reingresar a un colaborador -- tercer comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357) y primer ejercicio real de la invariante de
// no-solape. CA-ADR-0030: el aggregate declina con resultado (nunca lanza, nunca emite evento de
// fallo); el handler traduce la razon a InvalidOperationException (409) o KeyNotFoundException
// (404). El evento de exito es VinculacionIniciada, EL MISMO del registro (#330) -- cero tipos
// nuevos (CA-ADR-0029: un evento no conoce su comando).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction;
using Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ReingresarColaboradorFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que TerminarVinculacionCommandHandlerTests).
public class ReingresarColaboradorCommandHandlerTests : CommandHandlerAsyncTest<ReingresarColaborador>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC:79543210";

    private const string CodigoVinculacionOriginal = "COL-001";
    private const string CodigoReingreso = "COL-002";
    private static readonly DateOnly FechaInicioOriginal = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacionOriginal = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingresoValida =
        FechaEfectivaTerminacionOriginal.AddDays(1);

    protected override ICommandHandlerAsync<ReingresarColaborador> Handler =>
        new ReingresarColaboradorCommandHandler(EventStore);

    private static ReingresarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        CodigoColaborador: CodigoReingreso,
        FechaInicio: FechaInicioReingresoValida);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaOriginal() =>
        new(CodigoVinculacionOriginal, FechaInicioOriginal);

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion) -- CA-2
    // (vinculacion abierta desde el registro, nunca terminada).
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaOriginal());

    // Precondicion: colaborador registrado con la vinculacion original ya terminada en la fecha
    // dada -- base de CA-1, CA-3 y CA-4 (la fecha puede ser un registro tardio o un preaviso).
    private void DadoUnColaboradorConVinculacionTerminada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(fechaEfectiva));

    // CA-1: colaborador con la ultima vinculacion terminada + FechaInicio estrictamente posterior
    // a la FechaEfectiva -> el stream recibe VinculacionIniciada con el codigo nuevo; el aggregate
    // rehidratado refleja vinculacion abierta (codigo nuevo, sin terminacion registrada). Este es
    // el ajuste que #350 exige sobre Apply(VinculacionIniciada): debe reabrir la vinculacion.
    [Fact]
    public async Task ReingresarColaborador_EmiteVinculacionIniciada_CuandoFechaInicioEsPosteriorALaTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioReingresoValida);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-2: la vinculacion vigente esta abierta (recien registrada, nunca terminada) -> 409,
    // ningun evento nuevo en el stream, el estado conserva el codigo original.
    [Fact]
    public async Task ReingresarColaborador_LanzaInvalidOperationException_CuandoLaVinculacionVigenteEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ReingresarColaboradorCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-2 (segunda variante): un reingreso previo sigue abierto (ciclo completo
    // registro-terminacion-reingreso) -> 409 igual, la invariante de no-solape aplica sobre
    // CUALQUIER vinculacion vigente sin terminar, no solo la primera.
    [Fact]
    public async Task ReingresarColaborador_LanzaInvalidOperationException_CuandoElReingresoPrevioSigueAbierto()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));

        var segundoReingreso = ComandoValido() with { CodigoColaborador = "COL-003" };
        var act = async () => await WhenAsync(segundoReingreso);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ReingresarColaboradorCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
    }

    // CA-3: FechaInicio igual a la FechaEfectiva de la ultima terminacion -> 409 por no-solape (el
    // mismo dia se rechaza -- el dia de la fecha efectiva pertenece a la vinculacion que termina).
    [Fact]
    public async Task ReingresarColaborador_LanzaInvalidOperationException_CuandoFechaInicioEsIgualALaFechaEfectivaDeTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);

        var act = async () => await WhenAsync(
            ComandoValido() with { FechaInicio = FechaEfectivaTerminacionOriginal });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ReingresarColaboradorCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-3 (segunda direccion): FechaInicio anterior a la FechaEfectiva de terminacion -> 409
    // igual, con mayor margen de solape.
    [Fact]
    public async Task ReingresarColaborador_LanzaInvalidOperationException_CuandoFechaInicioEsAnteriorALaFechaEfectivaDeTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);
        var fechaAnterior = FechaEfectivaTerminacionOriginal.AddDays(-1);

        var act = async () => await WhenAsync(ComandoValido() with { FechaInicio = fechaAnterior });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ReingresarColaboradorCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-3 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea el reingreso
    // si su FechaInicio no supera la fecha del preaviso -- la regla de no-solape se compone sin
    // reloj (decision de refinamiento 2026-08-11).
    [Fact]
    public async Task ReingresarColaborador_LanzaInvalidOperationException_CuandoFechaInicioNoSuperaElPreavisoRegistrado()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConVinculacionTerminada(fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido() with { FechaInicio = fechaPreavisoFutura });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ReingresarColaboradorCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-4: un reingreso con FechaInicio posterior a la fecha del preaviso registrado procede sin
    // ninguna consulta al reloj del servidor -- el preaviso se resuelve componiendo CA-2 (no
    // abierta) + CA-3 (no-solape), sin regla nueva.
    [Fact]
    public async Task ReingresarColaborador_EmiteVinculacionIniciada_CuandoFechaInicioEsPosteriorAlPreavisoRegistrado()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        var fechaInicioReingreso = fechaPreavisoFutura.AddDays(1);
        DadoUnColaboradorConVinculacionTerminada(fechaPreavisoFutura);

        await WhenAsync(ComandoValido() with { FechaInicio = fechaInicioReingreso });

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, fechaInicioReingreso));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-5: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Sin And<>: el aggregate no existe en el TestStore
    // (GetAggregateRoot retorna null) -- Then sin eventos esperados ya demuestra "sin escribir
    // nada al event store" (mismo precedente que TerminarVinculacionCommandHandlerTests CA-5).
    [Fact]
    public async Task ReingresarColaborador_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{ReingresarColaboradorCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
