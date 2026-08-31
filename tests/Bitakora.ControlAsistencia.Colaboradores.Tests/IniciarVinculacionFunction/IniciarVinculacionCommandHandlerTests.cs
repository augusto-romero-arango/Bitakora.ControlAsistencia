// Issue #378 (MEF-ADR-0043 paso 1, absorbe #350): iniciar una vinculacion nueva sobre un
// colaborador existente -- create disfrazado, verificado contra la historia del stream: el evento
// de exito es VinculacionIniciada, EL MISMO del registro (#330) y del reingreso original (#350) --
// cero tipos nuevos (CA-ADR-0029: un evento no conoce su comando). CA-ADR-0030: el aggregate
// declina con resultado (nunca lanza, nunca emite evento de fallo); el handler traduce la razon a
// InvalidOperationException (409) o KeyNotFoundException (404). Reemplaza a
// ReingresarColaboradorCommandHandlerTests (issue #350) -- mismos escenarios, comando/handler/
// aggregate/enum renombrados en terminos de iniciar vinculacion (CA-4); "reingreso" sigue nombrando
// el escenario de negocio en nombres de test y comentarios (no la operacion).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.IniciarVinculacionFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC-79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que TerminarVinculacionCommandHandlerTests).
public class IniciarVinculacionCommandHandlerTests : CommandHandlerAsyncTest<IniciarVinculacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionOriginal = "COL-001";
    private const string CodigoReingreso = "COL-002";
    private static readonly DateOnly FechaInicioOriginal = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacionOriginal = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingresoValida =
        FechaEfectivaTerminacionOriginal.AddDays(1);

    private const string CodigoSedeAnterior = "MED";
    private const string CodigoSedeNueva = "BOG";

    protected override ICommandHandlerAsync<IniciarVinculacion> Handler =>
        new IniciarVinculacionCommandHandler(EventStore);

    private static IniciarVinculacion ComandoValido() => new(
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
    // dada -- base de CA-1 y CA-2 (la fecha puede ser un registro tardio o un preaviso).
    private void DadoUnColaboradorConVinculacionTerminada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(fechaEfectiva));

    // Precondicion de CA-3/CA-4: la vinculacion anterior tenia una sede asignada -- distinta de la
    // que trae el reingreso, para que el And posterior distinga "sede nueva asentada" de "sede
    // heredada por accidente".
    private void DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
        DateOnly fechaEfectiva, string codigoSedeAnterior) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new SedeAsignada(codigoSedeAnterior),
            new VinculacionTerminada(fechaEfectiva));

    // CA-1: colaborador con la ultima vinculacion terminada + FechaInicio estrictamente posterior
    // a la FechaEfectiva -> el stream recibe VinculacionIniciada con el codigo nuevo; el aggregate
    // rehidratado refleja vinculacion abierta (codigo nuevo, sin terminacion registrada).
    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciada_CuandoFechaInicioEsPosteriorALaTerminacion()
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

    // CA-1 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> alcanza el MISMO stream ("CC-79543210") y tiene exito. La
    // normalizacion del numero la garantiza Identificacion.Crear (#348); la del codigo de tipo
    // ("cc" -> "CC") la garantiza TipoIdentificacion.Desde (#371). Sin esa normalizacion el handler
    // computaria otra clave y responderia 404 sobre un colaborador que si existe.
    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
    }

    // CA-2: la vinculacion vigente esta abierta (recien registrada, nunca terminada) -> 409,
    // ningun evento nuevo en el stream, el estado conserva el codigo original.
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoLaVinculacionVigenteEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-2 (segunda variante): un reingreso previo sigue abierto (ciclo completo
    // registro-terminacion-reingreso) -> 409 igual, la invariante de no-solape aplica sobre
    // CUALQUIER vinculacion vigente sin terminar, no solo la primera.
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoElReingresoPrevioSigueAbierto()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida));

        var segundoReingreso = ComandoValido() with { CodigoColaborador = "COL-003" };
        var act = async () => await WhenAsync(segundoReingreso);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoReingreso);
    }

    // CA-2: FechaInicio igual a la FechaEfectiva de la ultima terminacion -> 409 por no-solape (el
    // mismo dia se rechaza -- el dia de la fecha efectiva pertenece a la vinculacion que termina).
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoFechaInicioEsIgualALaFechaEfectivaDeTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);

        var act = async () => await WhenAsync(
            ComandoValido() with { FechaInicio = FechaEfectivaTerminacionOriginal });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-2 (segunda direccion): FechaInicio anterior a la FechaEfectiva de terminacion -> 409
    // igual, con mayor margen de solape.
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoFechaInicioEsAnteriorALaFechaEfectivaDeTerminacion()
    {
        DadoUnColaboradorConVinculacionTerminada(FechaEfectivaTerminacionOriginal);
        var fechaAnterior = FechaEfectivaTerminacionOriginal.AddDays(-1);

        var act = async () => await WhenAsync(ComandoValido() with { FechaInicio = fechaAnterior });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-2 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea el reingreso
    // si su FechaInicio no supera la fecha del preaviso -- la regla de no-solape se compone sin
    // reloj (decision de refinamiento 2026-08-11, heredada de #350).
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoFechaInicioNoSuperaElPreavisoRegistrado()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConVinculacionTerminada(fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido() with { FechaInicio = fechaPreavisoFutura });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionOriginal);
    }

    // CA-1 (preaviso vencido): un reingreso con FechaInicio posterior a la fecha del preaviso
    // registrado procede sin ninguna consulta al reloj del servidor -- el preaviso se resuelve
    // componiendo CA-2 (no abierta) + no-solape, sin regla nueva.
    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciada_CuandoFechaInicioEsPosteriorAlPreavisoRegistrado()
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

    // CA-3: reingreso CON CodigoSede emite VinculacionIniciada con la sede DENTRO del evento;
    // rehidratado, el colaborador queda con la sede nueva aunque la vinculacion anterior tuviera
    // otra.
    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciadaConSede_CuandoCodigoSedeLlegaEnElComando()
    {
        DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
            FechaEfectivaTerminacionOriginal, CodigoSedeAnterior);

        await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeNueva });

        Then(StreamIdEsperado,
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida, CodigoSedeNueva));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeNueva);
    }

    // CA-4: reingreso SIN CodigoSede deja la vinculacion nueva sin sede -- "reingreso nace limpio"
    // sigue siendo el default, aunque la vinculacion anterior tuviera una sede asignada.
    [Fact]
    public async Task IniciarVinculacion_EmiteVinculacionIniciadaSinSede_CuandoCodigoSedeNoLlegaAunqueLaAnteriorTeniaSede()
    {
        DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
            FechaEfectivaTerminacionOriginal, CodigoSedeAnterior);

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionIniciada(CodigoReingreso, FechaInicioReingresoValida, null));
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, null);
    }

    // CA-7: la sede en el comando no agrega ni quita razones de rechazo -- vinculacion abierta
    // sigue en 409 sin eventos nuevos, y la sede vigente NO absorbe la del comando rechazado.
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoLaVinculacionVigenteEstaAbiertaYElComandoTraeSede()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new SedeAsignada(CodigoSedeAnterior));

        var act = async () => await WhenAsync(ComandoValido() with { CodigoSede = CodigoSedeNueva });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeAnterior);
    }

    // CA-7 (segunda razon de rechazo): el solape con la vinculacion anterior sigue en 409 aunque el
    // comando traiga sede, y la sede vigente queda intacta.
    [Fact]
    public async Task IniciarVinculacion_LanzaInvalidOperationException_CuandoFechaSolapaYElComandoTraeSede()
    {
        DadoUnColaboradorConVinculacionTerminadaYSedeAsignada(
            FechaEfectivaTerminacionOriginal, CodigoSedeAnterior);

        var act = async () => await WhenAsync(ComandoValido() with
        {
            FechaInicio = FechaEfectivaTerminacionOriginal,
            CodigoSede = CodigoSedeNueva
        });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string?>(StreamIdEsperado, c => c.CodigoSede, CodigoSedeAnterior);
    }

    // CA-3: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Sin And<>: el aggregate no existe en el TestStore
    // (GetAggregateRoot retorna null) -- Then sin eventos esperados ya demuestra "sin escribir
    // nada al event store" (mismo precedente que TerminarVinculacionCommandHandlerTests CA-5).
    [Fact]
    public async Task IniciarVinculacion_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{IniciarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
