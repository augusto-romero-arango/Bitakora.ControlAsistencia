// Issue #355: retirar una etiqueta dinamica -- octavo comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357), gemelo de AsignarEtiqueta sobre el mismo
// diccionario. CA-ADR-0030: no hay eventos de fallo -- el aggregate declina CON RESULTADO tanto la
// regla de apertura estricta (decision #1) como la categoria inexistente (decision #2: con
// categorias libres, un typo debe aflorar al instante -- SIN idempotencia silenciosa, a diferencia
// de AsignarEtiqueta).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que el resto de la cadena #349-#354).
public class RetirarEtiquetaCommandHandlerTests : CommandHandlerAsyncTest<RetirarEtiqueta>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC:79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private const string CodigoVinculacionReingreso = "COL-002";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);
    private static readonly DateOnly FechaInicioReingreso = new(2026, 7, 1);

    protected override ICommandHandlerAsync<RetirarEtiqueta> Handler =>
        new RetirarEtiquetaCommandHandler(EventStore);

    private static RetirarEtiqueta ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Categoria: "área");

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    // Precondicion: colaborador registrado con una vinculacion abierta y SIN etiquetas -- base de
    // CA-4.
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // Precondicion (CA-3): la vinculacion vigente ya tiene la etiqueta dada asignada.
    private void DadoUnColaboradorConEtiquetaAsignada(Etiqueta etiqueta) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiqueta));

    // Precondicion (CA-5): la vinculacion vigente tiene la etiqueta dada Y una terminacion
    // registrada -- incluye un preaviso con fecha futura, que bloquea igual sin distincion de
    // estado.
    private void DadoUnColaboradorConEtiquetaYTerminacionRegistrada(
        Etiqueta etiqueta, DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiqueta),
            new VinculacionTerminada(fechaEfectiva));

    // CA-3: retirar por una forma distinta de la que se asigno ("área" retira lo asignado como
    // "Area", misma categoria normalizada) -> el stream recibe EtiquetaRetirada con la categoria
    // normalizada; el aggregate ya no la refleja.
    [Fact]
    public async Task RetirarEtiqueta_EmiteEtiquetaRetirada_CuandoLaCategoriaExisteConFormaDistinta()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("Area", "Ventas"));

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaRetirada("area"));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // CA-3 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> el retiro alcanza el MISMO stream ("CC:79543210") y emite el
    // evento.
    [Fact]
    public async Task RetirarEtiqueta_EmiteEtiquetaRetirada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("Area", "Ventas"));
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new EtiquetaRetirada("area"));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // CA-4: retirar una categoria que nunca se asigno -> 409, ningun evento nuevo, el diccionario
    // de etiquetas queda intacto (vacio).
    [Fact]
    public async Task RetirarEtiqueta_LanzaInvalidOperationException_CuandoLaCategoriaNoExiste()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.CategoriaInexistente}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // CA-4 (el typo debe aflorar, decision #2 del issue): "Aera" no es "Area" -- categorias
    // distintas normalizadas, aunque exista una etiqueta para "Area" -> 409 igual, ningun evento,
    // la etiqueta existente ("Area") queda intacta.
    [Fact]
    public async Task RetirarEtiqueta_LanzaInvalidOperationException_CuandoHayUnErrorDeTranscripcionEnLaCategoria()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("Area", "Ventas"));

        var act = async () => await WhenAsync(ComandoValido() with { Categoria = "Aera" });

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.CategoriaInexistente}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    // CA-5 (decision #1, regla estricta de apertura): la ULTIMA vinculacion tiene terminacion
    // registrada -> 409, ningun evento nuevo, la etiqueta existente queda intacta.
    [Fact]
    public async Task RetirarEtiqueta_LanzaInvalidOperationException_CuandoLaUltimaVinculacionTieneTerminacionRegistrada()
    {
        DadoUnColaboradorConEtiquetaYTerminacionRegistrada(
            Etiqueta.Crear("Area", "Ventas"), FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    // CA-5 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual -- las
    // etiquetas describen la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
    [Fact]
    public async Task RetirarEtiqueta_LanzaInvalidOperationException_CuandoLaTerminacionEsUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConEtiquetaYTerminacionRegistrada(
            Etiqueta.Crear("Area", "Ventas"), fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
    }

    // CA-6 (reingreso nace limpio): la etiqueta pertenecia a la vinculacion ANTERIOR (congelada
    // tras la terminacion) -- la vinculacion vigente (el reingreso) no la hereda, asi que retirarla
    // encuentra la categoria inexistente -> 409, igual que cualquier categoria nunca asignada.
    [Fact]
    public async Task RetirarEtiqueta_LanzaInvalidOperationException_CuandoLaEtiquetaPerteneceALaVinculacionAnteriorTrasUnReingreso()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(Etiqueta.Crear("Area", "Ventas")),
            new VinculacionTerminada(FechaEfectivaTerminacion),
            new VinculacionIniciada(CodigoVinculacionReingreso, FechaInicioReingreso));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.CategoriaInexistente}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // CA-7: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir
    // nada al event store" (mismo precedente que AnularTerminacionCommandHandlerTests CA-5). Sin
    // And<>: el aggregate no existe en el TestStore (GetAggregateRoot lanzaria ArgumentNullException).
    [Fact]
    public async Task RetirarEtiqueta_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
