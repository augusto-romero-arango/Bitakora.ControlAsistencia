// Issue #352: corregir la fecha de inicio de la ULTIMA vinculacion de un colaborador (tenga o no
// terminacion registrada) -- quinto comando del ciclo de vida de ColaboradorAggregateRoot
// (desglose #348-#357). CA-ADR-0030: el aggregate combina "declinar con resultado" (dos razones
// 409: coherencia interna, no-solape hacia atras) con "declinar en silencio" (idempotencia,
// precedente CorregirNombres #351). Depende de #350 (reingresar): la no-solape hacia atras solo es
// ejercitable cuando existe una vinculacion anterior.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC-79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que TerminarVinculacionCommandHandlerTests/
// ReingresarColaboradorCommandHandlerTests/CorregirNombresCommandHandlerTests).
public class CorregirFechaInicioVinculacionCommandHandlerTests
    : CommandHandlerAsyncTest<CorregirFechaInicioVinculacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionOriginal = "COL-001";
    private const string CodigoReingreso = "COL-002";
    private const string CodigoSegundoReingreso = "COL-003";
    private static readonly DateOnly FechaInicioOriginal = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacionOriginal = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingreso =
        FechaEfectivaTerminacionOriginal.AddDays(1); // 2026-06-02
    private static readonly DateOnly FechaEfectivaTerminacionReingreso = new(2026, 9, 1);
    private static readonly DateOnly FechaInicioSegundoReingreso =
        FechaEfectivaTerminacionReingreso.AddDays(1); // 2026-09-02

    protected override ICommandHandlerAsync<CorregirFechaInicioVinculacion> Handler =>
        new CorregirFechaInicioVinculacionCommandHandler(EventStore);

    private static CorregirFechaInicioVinculacion ComandoCon(DateOnly fechaCorregida) => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        FechaCorregida: fechaCorregida);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaOriginal() =>
        new(CodigoVinculacionOriginal, FechaInicioOriginal);

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion) -- base
    // de CA-1 y CA-4.
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaOriginal());

    // Precondicion: colaborador registrado con la vinculacion original ya terminada -- base de
    // CA-2.
    private void DadoUnColaboradorConVinculacionTerminada() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal));

    // Precondicion (CA-3): colaborador con la vinculacion original terminada y un reingreso
    // posterior sin terminar -- la ULTIMA vinculacion (la del reingreso) es la que se corrige; la
    // original es la "vinculacion anterior" contra la que se ejerce la no-solape hacia atras.
    private void DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingreso));

    // Precondicion: la ULTIMA vinculacion es un reingreso que ya tiene su propia terminacion
    // registrada -- unico estado en que las DOS reglas de estado acotan la fecha a la vez
    // (ventana abierta-cerrada: FechaEfectivaTerminacionOriginal < fecha <= FechaEfectivaTerminacionReingreso).
    private void DadoUnColaboradorConReingresoYaTerminado() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingreso),
            new VinculacionTerminada(FechaEfectivaTerminacionReingreso));

    // Precondicion: dos reingresos encadenados -- la "vinculacion anterior" a la ultima es la del
    // PRIMER reingreso (terminada en FechaEfectivaTerminacionReingreso), no la original.
    private void DadoUnColaboradorConDosReingresos() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingreso),
            new VinculacionTerminada(FechaEfectivaTerminacionReingreso),
            new VinculacionIniciada(CodigoSegundoReingreso, FechaInicioSegundoReingreso));

    // CA-1: la ultima vinculacion esta ABIERTA + FechaCorregida distinta valida -> el stream
    // recibe FechaInicioVinculacionCorregida; el aggregate rehidratado refleja la fecha nueva.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoLaUltimaVinculacionEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);

        await WhenAsync(ComandoCon(fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
    }

    // CA-1 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> la correccion alcanza el MISMO stream ("CC-79543210") y emite
    // el evento. La normalizacion del numero la garantiza Identificacion.Crear (#348); la del
    // codigo de tipo ("cc" -> "CC") la garantiza TipoIdentificacion.Desde, que normaliza
    // internamente (issue #371 -- supersede el racional de #348, ver TipoIdentificacionTests).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);
        var comandoSinNormalizar = ComandoCon(fechaCorregida) with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
    }

    // CA-2 (primera direccion): la ultima vinculacion tiene terminacion registrada y
    // FechaCorregida es estrictamente ANTERIOR a esa FechaEfectiva -> exito. La terminacion previa
    // no se toca por esta correccion (Tell-don't-Ask: la correccion es ortogonal a la vigencia,
    // MEF-ADR-0012).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsAnteriorALaFechaEfectivaPropia()
    {
        DadoUnColaboradorConVinculacionTerminada();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);

        await WhenAsync(ComandoCon(fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacionOriginal);
    }

    // CA-2 (segunda direccion, borde valido): FechaCorregida IGUAL a la FechaEfectiva propia ->
    // exito -- vinculacion de un solo dia, consistente con TerminarVinculacion (#349).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsIgualALaFechaEfectivaPropia()
    {
        DadoUnColaboradorConVinculacionTerminada();

        await WhenAsync(ComandoCon(FechaEfectivaTerminacionOriginal));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(FechaEfectivaTerminacionOriginal));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaEfectivaTerminacionOriginal);
    }

    // CA-2 (borde invalido): FechaCorregida POSTERIOR a la FechaEfectiva propia -> 409, ningun
    // evento nuevo, el estado conserva la fecha de inicio original.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaInvalidOperationException_CuandoFechaCorregidaEsPosteriorALaFechaEfectivaPropia()
    {
        DadoUnColaboradorConVinculacionTerminada();

        var act = async () => await WhenAsync(
            ComandoCon(FechaEfectivaTerminacionOriginal.AddDays(1)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaPosteriorATerminacionPropia}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    // CA-1 (variante tras reingreso, dependencia #350): la ULTIMA vinculacion es la del reingreso
    // y esta abierta; FechaCorregida estrictamente posterior a la FechaEfectiva de la vinculacion
    // anterior y distinta de la actual -> exito. Ejercita el Apply(VinculacionIniciada)
    // re-aplicado que #350 construyo.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoLaUltimaVinculacionEsUnReingresoAbierto()
    {
        DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto();
        var fechaCorregida = FechaEfectivaTerminacionOriginal.AddDays(2); // 2026-06-03

        await WhenAsync(ComandoCon(fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
    }

    // CA-3 (primera direccion, borde): tras un reingreso, FechaCorregida IGUAL a la FechaEfectiva
    // de la vinculacion anterior -> 409 por no-solape (el mismo dia se rechaza -- el dia de la
    // fecha efectiva pertenece a la vinculacion que termino, misma frontera que Reingresar #350).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaInvalidOperationException_CuandoFechaCorregidaEsIgualALaFechaEfectivaDeLaVinculacionAnterior()
    {
        DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto();

        var act = async () => await WhenAsync(ComandoCon(FechaEfectivaTerminacionOriginal));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioReingreso);
    }

    // CA-3 (segunda direccion, mayor margen): FechaCorregida ANTERIOR a la FechaEfectiva de la
    // vinculacion anterior -> 409 igual.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaInvalidOperationException_CuandoFechaCorregidaEsAnteriorALaFechaEfectivaDeLaVinculacionAnterior()
    {
        DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto();

        var act = async () => await WhenAsync(
            ComandoCon(FechaEfectivaTerminacionOriginal.AddDays(-1)));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioReingreso);
    }

    // CA-2 + CA-3 combinados: la ULTIMA vinculacion es un reingreso que YA tiene terminacion
    // propia, unico estado en que las dos reglas de estado acotan la fecha a la vez. Una fecha
    // dentro de la ventana valida (posterior a la terminacion ANTERIOR, anterior o igual a la
    // PROPIA) se acepta, y la terminacion propia sigue intacta -- la correccion es ortogonal a la
    // vigencia (MEF-ADR-0012).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoLaFechaCaeEntreLaTerminacionAnteriorYLaPropia()
    {
        DadoUnColaboradorConReingresoYaTerminado();
        // 2026-06-11: dentro de (2026-06-01, 2026-09-01] y distinta de la actual (2026-06-02), que
        // seria idempotencia y no ejercitaria ninguna de las dos reglas.
        var fechaCorregida = FechaEfectivaTerminacionOriginal.AddDays(10);

        await WhenAsync(ComandoCon(fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacionReingreso);
    }

    // CA-3 (cadena larga): con DOS reingresos encadenados, la no-solape se evalua contra la
    // terminacion de la vinculacion INMEDIATAMENTE anterior (la del primer reingreso,
    // 2026-09-01), no contra la de la vinculacion original (2026-06-01). Una fecha posterior a la
    // terminacion original pero anterior a la del primer reingreso debe rechazarse: si la
    // terminacion anterior se congelara en la primera, este comando pasaria y dejaria dos
    // vinculaciones solapadas en el stream.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaInvalidOperationException_CuandoFechaCorregidaSolapaElReingresoPrevioYNoLaVinculacionOriginal()
    {
        DadoUnColaboradorConDosReingresos();
        var fechaCorregida = new DateOnly(2026, 7, 1); // > 2026-06-01 pero < 2026-09-01

        var act = async () => await WhenAsync(ComandoCon(fechaCorregida));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioSegundoReingreso);
    }

    // CA-4: FechaCorregida IGUAL a la fecha de inicio actual (vinculacion abierta) -> idempotencia
    // silenciosa: ningun evento nuevo en el stream, el estado conserva la fecha original.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_NoEmiteEvento_CuandoFechaCorregidaEsIgualALaActual()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoCon(FechaInicioOriginal));

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    // CA-4 (variante con terminacion registrada): la idempotencia se evalua ANTES que las demas
    // reglas -- una FechaCorregida igual a la actual no dispara la validacion de coherencia
    // interna aunque la ultima vinculacion ya este terminada.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_NoEmiteEvento_CuandoFechaCorregidaEsIgualALaActualYLaVinculacionEstaTerminada()
    {
        DadoUnColaboradorConVinculacionTerminada();

        await WhenAsync(ComandoCon(FechaInicioOriginal));

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    // CA-5: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir
    // nada al event store" (mismo precedente que TerminarVinculacionCommandHandlerTests CA-5 /
    // ReingresarColaboradorCommandHandlerTests CA-5 / CorregirNombresCommandHandlerTests CA-4).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoCon(FechaInicioOriginal));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
