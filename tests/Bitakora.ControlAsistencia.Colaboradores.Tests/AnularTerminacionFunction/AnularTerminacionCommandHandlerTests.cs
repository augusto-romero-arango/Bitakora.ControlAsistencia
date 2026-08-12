// Issue #354: anular la terminacion de una vinculacion -- sexto comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357) y el mas simple de la cadena: una sola regla, cero
// fechas en el payload. CA-ADR-0030: el aggregate declina con resultado (nunca lanza, nunca emite
// evento de fallo); el handler traduce la razon a InvalidOperationException (409) o
// KeyNotFoundException (404). Resuelve el arrepentimiento del preaviso (Maria anuncio su salida al
// 30 y el 27 decide quedarse) y compone con TerminarVinculacion (#349) la correccion de una fecha
// de terminacion errada -- ver ComposicionAnularYTerminarVinculacionTests para el segundo tramo de
// esa composicion (CA-2).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que el resto de la cadena #349-#352).
public class AnularTerminacionCommandHandlerTests : CommandHandlerAsyncTest<AnularTerminacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC:79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);

    protected override ICommandHandlerAsync<AnularTerminacion> Handler =>
        new AnularTerminacionCommandHandler(EventStore);

    private static AnularTerminacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion) -- base
    // de CA-3.
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // Precondicion: colaborador con la vinculacion vigente ya terminada en la fecha dada -- base de
    // CA-1 (incluye el caso de un preaviso con fecha futura, que bloquea igual sin distincion de
    // estado).
    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    // Precondicion (CA-3, decision #4 del issue): la terminacion ya fue anulada antes -- una
    // segunda anulacion encuentra la vinculacion abierta, sin distincion respecto de "nunca
    // terminada".
    private void DadoUnColaboradorConTerminacionYaAnulada(DateOnly fechaEfectivaOriginal) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectivaOriginal),
            new TerminacionAnulada());

    // CA-1: la ultima vinculacion tiene terminacion registrada + POST valido -> el stream recibe
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

    // CA-1 (el arrepentimiento del preaviso, decision #2 del issue): un preaviso con fecha futura
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

    // CA-1 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> la anulacion alcanza el MISMO stream ("CC:79543210") y tiene
    // exito. La normalizacion del numero la garantiza Identificacion.Crear (#348); la del codigo de
    // tipo ("cc" -> "CC") es responsabilidad del handler en el borde, porque TipoIdentificacion.Desde
    // es case-sensitive por diseno (#348).
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

    // CA-3: la ultima vinculacion nunca ha sido terminada (recien registrada) -> 409, ningun evento
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

    // CA-3 (decision #4 del issue, idempotencia por rechazo): anular dos veces -> la segunda
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

    // CA-4 (decision #3 del issue, aprobada explicitamente): tras un reingreso, la terminacion de
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
            new VinculacionIniciada("COL-002", fechaInicioReingreso));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, "COL-002");
    }

    // CA-5: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir
    // nada al event store" (mismo precedente que TerminarVinculacionCommandHandlerTests CA-5 /
    // ReingresarColaboradorCommandHandlerTests CA-5).
    [Fact]
    public async Task AnularTerminacion_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
