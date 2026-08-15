// Issue #354: anular la terminacion de una vinculacion -- sexto comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357) y el mas simple de la cadena: una sola regla, cero
// fechas en el payload. CA-ADR-0030: el aggregate declina con resultado (nunca lanza, nunca emite
// evento de fallo); el handler traduce la razon a InvalidOperationException (409) o
// KeyNotFoundException (404). Resuelve el arrepentimiento del preaviso (Maria anuncio su salida al
// 30 y el 27 decide quedarse) y compone con TerminarVinculacion (#349) la correccion de una fecha
// de terminacion errada -- ver ComposicionAnularYTerminarVinculacionTests para el segundo tramo de
// esa composicion (CA-2).
// Issue #379 (MEF-ADR-0043 paso 4, CA-5): el comando gana el campo Codigo -- el {codigo} de la ruta
// HTTP, comparado por el aggregate contra la vinculacion vigente ANTES que la regla de estado.
// CodigoNoCorresponde -> 409 (no 404: es conflicto con el estado vigente, no un recurso inexistente).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC-79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que el resto de la cadena #349-#352).
public class AnularTerminacionCommandHandlerTests : CommandHandlerAsyncTest<AnularTerminacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private const string CodigoVinculacionReingreso = "COL-002";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);

    protected override ICommandHandlerAsync<AnularTerminacion> Handler =>
        new AnularTerminacionCommandHandler(EventStore);

    private static AnularTerminacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Codigo: CodigoVinculacionVigente);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion) -- base
    // de CA-4.
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // Precondicion: colaborador con la vinculacion vigente ya terminada en la fecha dada -- base de
    // CA-2 (incluye el caso de un preaviso con fecha futura, que bloquea igual sin distincion de
    // estado).
    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    // Precondicion (CA-4, decision #4 del issue): la terminacion ya fue anulada antes -- una
    // segunda anulacion encuentra la vinculacion abierta, sin distincion respecto de "nunca
    // terminada".
    private void DadoUnColaboradorConTerminacionYaAnulada(DateOnly fechaEfectivaOriginal) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectivaOriginal),
            new TerminacionAnulada());

    // CA-2: la ultima vinculacion tiene terminacion registrada + POST valido -> el stream recibe
    // TerminacionAnulada; el aggregate rehidratado refleja la vinculacion abierta con su codigo y
    // fecha de inicio ORIGINALES intactos. (El registro en
    // IdentidadEventosColaboradores.TiposPersistidos lo cubren AliasEventosColaboradoresTests/
    // ComposicionServiciosTests, no este handler.)
    [Fact]
    public async Task AnularTerminacion_EmiteTerminacionAnulada_CuandoLaUltimaVinculacionTieneTerminacionRegistrada()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new TerminacionAnulada());
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionVigente);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioVinculacionVigente);
    }

    // CA-2 (el arrepentimiento del preaviso, decision #2 del issue): un preaviso con fecha futura
    // ya registrado se anula igual que uno vencido -- "tiene terminacion registrada" se evalua sin
    // reloj, sin importar si la fecha efectiva ya paso.
    [Fact]
    public async Task AnularTerminacion_EmiteTerminacionAnulada_CuandoLaTerminacionEsUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConTerminacionRegistrada(fechaPreavisoFutura);

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new TerminacionAnulada());
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-2 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> la anulacion alcanza el MISMO stream ("CC-79543210") y tiene
    // exito. La normalizacion del numero la garantiza Identificacion.Crear (#348); la del codigo de
    // tipo ("cc" -> "CC") la garantiza TipoIdentificacion.Desde, que normaliza internamente (issue
    // #371 -- supersede el racional de #348, ver TipoIdentificacionTests).
    [Fact]
    public async Task AnularTerminacion_EmiteTerminacionAnulada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new TerminacionAnulada());
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-2 + CA-3 (cara de exito de la misma regla): la ULTIMA vinculacion nacio de un reingreso y
    // SI tiene terminacion registrada -> se anula esa, no la anterior. Unico estado del stream
    // donde conviven las dos terminaciones (la congelada de la vinculacion anterior, #350/#352, y
    // la de la vigente), asi que es el que demuestra que Apply(TerminacionAnulada) reabre solo la
    // ULTIMA y deja intacto su codigo y su fecha de inicio (los del reingreso, no los originales).
    // El codigo correcto ahora es el del REINGRESO -- CA-5 exige direccionar la ULTIMA vinculacion.
    [Fact]
    public async Task AnularTerminacion_EmiteTerminacionAnulada_CuandoLaUltimaVinculacionNacioDeUnReingresoYaTerminado()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        var fechaTerminacionReingreso = new DateOnly(2026, 9, 30);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada(CodigoVinculacionReingreso, fechaInicioReingreso),
            new VinculacionTerminada(fechaTerminacionReingreso));

        await WhenAsync(ComandoValido() with { Codigo = CodigoVinculacionReingreso });

        Then(StreamIdEsperado, new TerminacionAnulada());
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionReingreso);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaInicioReingreso);
    }

    // CA-5 (GATE, evaluada PRIMERO): el codigo del comando no corresponde al de la vinculacion
    // vigente -> 409 con la razon CodigoNoCorresponde, ningun evento nuevo, el estado no cambia.
    [Fact]
    public async Task AnularTerminacion_LanzaInvalidOperationException_CuandoElCodigoNoCorrespondeALaVinculacionVigente()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = "COL-999" });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacion);
    }

    // CA-5 (orden de evaluacion): el codigo equivocado se rechaza AUNQUE la vinculacion vigente
    // este abierta -- el direccionamiento precede a las reglas de estado; un comando dirigido a la
    // vinculacion equivocada no debe filtrar que la vigente esta abierta.
    [Fact]
    public async Task AnularTerminacion_LanzaInvalidOperationExceptionPorCodigo_CuandoElCodigoNoCorrespondeYLaVinculacionEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = "COL-999" });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // MEF-ADR-0004 capa 4 (ADR aplicable declarado en el issue): rehidratar un stream con orden
    // anomalo -- una TerminacionAnulada sin VinculacionTerminada previa -- no lanza. Si Apply
    // lanzara, el aggregate quedaria permanentemente roto y ningun evento posterior podria
    // repararlo; aqui simplemente vuelve a dejar la vinculacion abierta, y el comando se rechaza
    // por la unica regla, como cualquier otra vinculacion abierta.
    [Fact]
    public async Task AnularTerminacion_LanzaInvalidOperationException_CuandoElStreamTraeUnaAnulacionSinTerminacionPrevia()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new TerminacionAnulada());

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionVigente);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioVinculacionVigente);
    }

    // CA-4: la ultima vinculacion nunca ha sido terminada (recien registrada) -> 409, ningun evento
    // nuevo en el stream, el estado conserva la vinculacion abierta.
    [Fact]
    public async Task AnularTerminacion_LanzaInvalidOperationException_CuandoLaVinculacionNuncaHaSidoTerminada()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-4 (decision #4 del issue, idempotencia por rechazo): anular dos veces -> la segunda
    // encuentra la vinculacion abierta (por la primera anulacion) -> 409 igual, sin distincion
    // respecto de "nunca terminada".
    [Fact]
    public async Task AnularTerminacion_LanzaInvalidOperationException_CuandoLaTerminacionYaFueAnuladaAntes()
    {
        DadoUnColaboradorConTerminacionYaAnulada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // CA-3 (decision #3 del issue, aprobada explicitamente): tras un reingreso, la terminacion de
    // la vinculacion ANTERIOR queda CONGELADA -- la ULTIMA vinculacion (la del reingreso) es la que
    // cuenta, y esta abierta -> 409. Anularla reabriria una vinculacion teniendo otra abierta, lo
    // que la invariante de no-solape prohibe.
    [Fact]
    public async Task AnularTerminacion_LanzaInvalidOperationException_CuandoLaUltimaVinculacionNacioDeUnReingresoSinTerminar()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada(CodigoVinculacionReingreso, fechaInicioReingreso));

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = CodigoVinculacionReingreso });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionReingreso);
    }

    // CA-6: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir
    // nada al event store" (mismo precedente que TerminarVinculacionCommandHandlerTests CA-6 /
    // IniciarVinculacionCommandHandlerTests CA-3).
    [Fact]
    public async Task AnularTerminacion_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
