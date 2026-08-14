// Issue #355: asignar una etiqueta dinamica -- septimo comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357). CA-ADR-0030: no hay eventos de fallo -- el
// aggregate declina CON RESULTADO la regla de apertura estricta (decision #1: la ULTIMA
// vinculacion no puede tener terminacion registrada, incluido un preaviso sin vencer) y declina EN
// SILENCIO la idempotencia (CA-2: etiqueta identica por valor, decision #3).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC-79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que el resto de la cadena #349-#354).
public class AsignarEtiquetaCommandHandlerTests : CommandHandlerAsyncTest<AsignarEtiqueta>
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

    protected override ICommandHandlerAsync<AsignarEtiqueta> Handler =>
        new AsignarEtiquetaCommandHandler(EventStore);

    private static AsignarEtiqueta ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Categoria: "Área",
        Valor: "Tecnología");

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaVigente() =>
        new(CodigoVinculacionVigente, FechaInicioVinculacionVigente);

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion) -- base
    // de CA-1.
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // Precondicion (CA-5): la vinculacion vigente ya tiene una terminacion registrada -- incluye un
    // preaviso con fecha futura, que bloquea igual sin distincion de estado.
    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    // Precondicion (CA-2): la vinculacion vigente ya tiene una etiqueta asignada para esa categoria.
    private void DadoUnColaboradorConEtiquetaAsignada(Etiqueta etiqueta) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiqueta));

    // CA-1: categoria nueva + vinculacion abierta -> el stream recibe EtiquetaAsignada con la doble
    // forma; el aggregate rehidratado refleja la etiqueta bajo su categoria normalizada.
    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoLaCategoriaEsNueva()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var etiquetaEsperada = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaEsperada));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaEsperada);
    }

    // CA-1 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> la asignacion alcanza el MISMO stream ("CC-79543210") y emite el
    // evento.
    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var comandoSinNormalizar = ComandoValido() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología")));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
    }

    // CA-2: asignar sobre una categoria existente (via EsMismaCategoria: "Área" sobre "area") con
    // un valor distinto sobrescribe -- un valor por categoria, nunca duplica.
    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoLaCategoriaExisteConValorDistinto()
    {
        DadoUnColaboradorConEtiquetaAsignada(Etiqueta.Crear("area", "Ventas"));
        var etiquetaSobrescrita = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaSobrescrita));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaSobrescrita);
    }

    // CA-2: la etiqueta del comando es IGUAL por valor (Etiqueta.Equals, #353) a la ya asignada
    // para esa categoria -> idempotencia silenciosa: ningun evento nuevo, el estado conserva la
    // etiqueta original.
    [Fact]
    public async Task AsignarEtiqueta_NoEmiteEvento_CuandoLaEtiquetaEsIgualPorValorALaExistente()
    {
        var etiquetaExistente = Etiqueta.Crear("Área", "Tecnología");
        DadoUnColaboradorConEtiquetaAsignada(etiquetaExistente);

        // Misma etiqueta por valor, con otra combinacion de mayusculas/tildes en ambos campos.
        await WhenAsync(ComandoValido() with { Categoria = "area", Valor = "tecnologia" });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaExistente);
    }

    // CA-5 (decision #1, regla estricta de apertura): la ULTIMA vinculacion tiene terminacion
    // registrada -> 409, ningun evento nuevo, el diccionario de etiquetas queda intacto.
    [Fact]
    public async Task AsignarEtiqueta_LanzaInvalidOperationException_CuandoLaUltimaVinculacionTieneTerminacionRegistrada()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // CA-5 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual -- las
    // etiquetas describen la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
    [Fact]
    public async Task AsignarEtiqueta_LanzaInvalidOperationException_CuandoLaTerminacionEsUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        DadoUnColaboradorConTerminacionRegistrada(fechaPreavisoFutura);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 0);
    }

    // CA-2 (un valor por CATEGORIA, no una etiqueta por vinculacion): asignar una categoria nueva
    // cuando ya existe otra distinta las hace convivir -- la sobrescritura es por categoria, no un
    // reemplazo del diccionario completo. Agregado en revision: ningun test ejercia dos categorias
    // simultaneas, asi que un Apply que reemplazara el diccionario en vez de indexarlo habria
    // pasado en verde.
    [Fact]
    public async Task AsignarEtiqueta_ConservaLaCategoriaPrevia_CuandoLaCategoriaNuevaEsDistinta()
    {
        var etiquetaPrevia = Etiqueta.Crear("Sede", "Medellín");
        DadoUnColaboradorConEtiquetaAsignada(etiquetaPrevia);
        var etiquetaNueva = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaNueva));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 2);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["sede"], etiquetaPrevia);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaNueva);
    }

    // CA-5 x CA-2 (cruce que ningun test cubria; agregado en revision): la vinculacion tiene
    // terminacion registrada Y la etiqueta del comando es identica por valor a la ya asignada. CA-5
    // es incondicional ("409 en AMBOS comandos"), asi que la regla de apertura gana sobre la
    // idempotencia silenciosa -- orden deliberadamente INVERSO al de CorregirFechaInicio (#352),
    // donde SinCambios se evalua primero porque alli la correccion de nombres/fechas es valida sobre
    // una vinculacion cerrada. Aqui no: escribir etiquetas sobre una relacion laboral congelada se
    // rechaza aunque el estado deseado coincida.
    [Fact]
    public async Task AsignarEtiqueta_LanzaInvalidOperationException_CuandoLaEtiquetaEsIgualPeroLaVinculacionTieneTerminacionRegistrada()
    {
        var etiquetaExistente = Etiqueta.Crear("Área", "Tecnología");
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(etiquetaExistente),
            new VinculacionTerminada(FechaEfectivaTerminacion));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaExistente);
    }

    // CA-6 (reingreso nace limpio): tras un reingreso, la vinculacion nueva no hereda las etiquetas
    // de la anterior -- asignar sobre la vinculacion vigente crea la categoria desde cero, sin
    // colisionar con la etiqueta congelada de la vinculacion previa.
    [Fact]
    public async Task AsignarEtiqueta_EmiteEtiquetaAsignada_CuandoLaVinculacionEsUnReingresoTrasUnaTerminacionConEtiquetasPrevias()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new EtiquetaAsignada(Etiqueta.Crear("Área", "Ventas")),
            new VinculacionTerminada(FechaEfectivaTerminacion),
            new VinculacionIniciada(CodigoVinculacionReingreso, FechaInicioReingreso));
        var etiquetaEsperada = Etiqueta.Crear("Área", "Tecnología");

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new EtiquetaAsignada(etiquetaEsperada));
        And<ColaboradorAggregateRoot, int>(StreamIdEsperado, c => c.Etiquetas.Count, 1);
        And<ColaboradorAggregateRoot, Etiqueta>(
            StreamIdEsperado, c => c.Etiquetas["area"], etiquetaEsperada);
    }

    // CA-7: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir
    // nada al event store" (mismo precedente que AnularTerminacionCommandHandlerTests CA-5). Sin
    // And<>: el aggregate no existe en el TestStore (GetAggregateRoot lanzaria ArgumentNullException).
    [Fact]
    public async Task AsignarEtiqueta_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
