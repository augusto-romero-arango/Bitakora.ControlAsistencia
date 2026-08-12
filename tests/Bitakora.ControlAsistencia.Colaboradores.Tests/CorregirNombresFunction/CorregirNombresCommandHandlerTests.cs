// Issue #351: corregir los nombres de un colaborador -- cuarto comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357) y el mas simple: sin reglas de estado.
// CA-ADR-0030: no hay eventos de fallo -- el handler solo traduce "colaborador inexistente" a
// KeyNotFoundException (404). El aggregate declina en SILENCIO (idempotencia por igualdad de
// valor, decision de refinamiento 2026-08-11) cuando el nombre nuevo es igual por valor al actual.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

// El aggregate usa un stream ID compuesto (Identificacion.ToString(), "CC:79543210"), no el
// GuidAggregateId del harness -- overloads explicitos de Given/Then/And (regla 18 del
// test-writer, mismo criterio que TerminarVinculacionCommandHandlerTests/
// ReingresarColaboradorCommandHandlerTests).
public class CorregirNombresCommandHandlerTests : CommandHandlerAsyncTest<CorregirNombres>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002 + MEF-ADR-0037): literal, no
    // derivado de ColaboradorAggregateRoot.ComputarStreamId.
    private const string StreamIdEsperado = "CC:79543210";

    private const string CodigoVinculacionOriginal = "COL-001";
    private static readonly DateOnly FechaInicioOriginal = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaTerminacion = new(2026, 6, 1);

    protected override ICommandHandlerAsync<CorregirNombres> Handler =>
        new CorregirNombresCommandHandler(EventStore);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    // Nombre original del colaborador registrado -- oraculo del "actual" contra el que el
    // aggregate compara por igualdad de valor.
    private static NombreColaborador NombreOriginal() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreOriginal());

    private static VinculacionIniciada VinculacionIniciadaOriginal() =>
        new(CodigoVinculacionOriginal, FechaInicioOriginal);

    private static CorregirNombres ComandoConNombreDistinto() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        PrimerNombre: "Luis",
        SegundoNombre: "Alberto", // distinto del original ("Augusto")
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private static CorregirNombres ComandoConElMismoNombre() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    // Precondicion: colaborador registrado con una vinculacion abierta (sin terminacion).
    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaOriginal());

    // Precondicion (CA-2): colaborador registrado con la vinculacion ya terminada.
    private void DadoUnColaboradorConVinculacionTerminada() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacion));

    // CA-1: colaborador existente + comando con nombres distintos -> el stream recibe
    // NombresCorregidos con el NombreColaborador nuevo; el aggregate rehidratado refleja el
    // NombreCompleto corregido.
    [Fact]
    public async Task CorregirNombres_EmiteNombresCorregidos_CuandoElNombreEsDistintoPorValor()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var nombreEsperado = NombreColaborador.Crear("Luis", "Alberto", "Barreto", null);

        await WhenAsync(ComandoConNombreDistinto());

        Then(StreamIdEsperado, new NombresCorregidos(nombreEsperado));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Alberto Barreto");
    }

    // CA-1 (borde de identidad, MEF-ADR-0037): "cc" en minusculas + numero con espacios sobre un
    // colaborador ya registrado -> la correccion alcanza el MISMO stream ("CC:79543210") y emite el
    // evento. La normalizacion del numero la garantiza Identificacion.Crear (#348); la del codigo
    // de tipo ("cc" -> "CC") la garantiza TipoIdentificacion.Desde, que normaliza internamente
    // (issue #371 -- supersede el racional de #348, ver TipoIdentificacionTests). Sin esa
    // normalizacion el handler computaria otra clave y responderia 404 sobre un colaborador que si
    // existe.
    [Fact]
    public async Task CorregirNombres_EmiteNombresCorregidos_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var comandoSinNormalizar = ComandoConNombreDistinto() with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new NombresCorregidos(NombreColaborador.Crear("Luis", "Alberto", "Barreto", null)));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Alberto Barreto");
    }

    // CA-2: la vinculacion vigente esta TERMINADA -> la correccion procede igual -- solo exige
    // existencia del colaborador, nunca vigencia de la vinculacion (decision de refinamiento
    // 2026-08-11: los nombres son de la PERSONA, no de la vinculacion).
    [Fact]
    public async Task CorregirNombres_EmiteNombresCorregidos_CuandoLaVinculacionEstaTerminada()
    {
        DadoUnColaboradorConVinculacionTerminada();
        var nombreEsperado = NombreColaborador.Crear("Luis", "Alberto", "Barreto", null);

        await WhenAsync(ComandoConNombreDistinto());

        Then(StreamIdEsperado, new NombresCorregidos(nombreEsperado));
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Alberto Barreto");
        // La terminacion previa no se toca por esta correccion (Tell-don't-Ask: la correccion es
        // ortogonal a la vinculacion, MEF-ADR-0012).
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacion);
    }

    // CA-3: el nombre del comando es IGUAL por valor al actual -> idempotencia silenciosa: ningun
    // evento nuevo en el stream, el estado conserva el nombre original.
    [Fact]
    public async Task CorregirNombres_NoEmiteEvento_CuandoElNombreEsIgualPorValorAlActual()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoConElMismoNombre());

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Augusto Barreto");
    }

    // CA-3 (variante de opcionales): un segundo apellido que llega whitespace se normaliza a
    // ausente igual que el original (NombreColaborador.Crear, #348) -> sigue siendo "igual por
    // valor", idempotencia silenciosa -- la comparacion ocurre DESPUES de normalizar, no sobre los
    // primitivos crudos del comando.
    [Fact]
    public async Task CorregirNombres_NoEmiteEvento_CuandoElSegundoApellidoLlegaComoWhitespaceYElOriginalEsAusente()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoConElMismoNombre() with { SegundoApellido = "   " });

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.Nombre.NombreCompleto, "Luis Augusto Barreto");
    }

    // CA-4: colaborador inexistente -> 404 (KeyNotFoundException), sin escribir nada al event
    // store. Sin Given: el stream no existe. Then sin eventos esperados demuestra "sin escribir
    // nada al event store" (mismo precedente que TerminarVinculacionCommandHandlerTests CA-5 /
    // ReingresarColaboradorCommandHandlerTests CA-5).
    [Fact]
    public async Task CorregirNombres_LanzaKeyNotFoundException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoConNombreDistinto());

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{CorregirNombresCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
