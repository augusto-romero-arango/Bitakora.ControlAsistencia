// La correccion de una fecha de terminacion errada compone este comando con TerminarVinculacion: ver
// ComposicionAnularYTerminarVinculacionTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

// Stream ID compuesto (Identificacion.ToString()), no el GuidAggregateId del harness: de ahi los
// overloads explicitos de Given/Then/And.
public class AnularTerminacionCommandHandlerTests : CommandHandlerAsyncTest<AnularTerminacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente (MEF-ADR-0002): literal, no derivado de ColaboradorAggregateRoot.ComputarStreamId.
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

    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaVigente());

    // Incluye el preaviso con fecha futura, que bloquea igual.
    private void DadoUnColaboradorConTerminacionRegistrada(DateOnly fechaEfectiva) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectiva));

    private void DadoUnColaboradorConTerminacionYaAnulada(DateOnly fechaEfectivaOriginal) =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaEfectivaOriginal),
            new TerminacionAnulada());

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

    // Se evalua sin reloj: un preaviso futuro se anula igual que uno vencido.
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

    // Normalizan Identificacion.Crear (el numero) y TipoIdentificacion.Desde (el tipo).
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

    // Unico estado donde conviven dos terminaciones: demuestra que Apply(TerminacionAnulada) reabre solo la
    // ULTIMA y conserva su codigo y fecha de inicio (los del reingreso).
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

    [Fact]
    public async Task AnularTerminacion_LanzaReglaDeNegocioDeclinadaException_CuandoElCodigoNoCorrespondeALaVinculacionVigente()
    {
        DadoUnColaboradorConTerminacionRegistrada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = "COL-999" });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacion);
    }

    // El direccionamiento precede a las reglas de estado: un comando a la vinculacion equivocada no debe
    // filtrar que la vigente esta abierta.
    [Fact]
    public async Task AnularTerminacion_LanzaReglaDeNegocioDeclinadaExceptionPorCodigo_CuandoElCodigoNoCorrespondeYLaVinculacionEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = "COL-999" });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // Un stream con orden anomalo no debe hacer lanzar a Apply (MEF-ADR-0004): el aggregate quedaria roto
    // para siempre. La vinculacion queda abierta y el comando se rechaza como cualquier otra abierta.
    [Fact]
    public async Task AnularTerminacion_LanzaReglaDeNegocioDeclinadaException_CuandoElStreamTraeUnaAnulacionSinTerminacionPrevia()
    {
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new TerminacionAnulada());

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionVigente);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioVinculacionVigente);
    }

    [Fact]
    public async Task AnularTerminacion_LanzaReglaDeNegocioDeclinadaException_CuandoLaVinculacionNuncaHaSidoTerminada()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // Sin idempotencia silenciosa (decision #4 del issue #354): la segunda anulacion encuentra la
    // vinculacion abierta y se rechaza igual que "nunca terminada".
    [Fact]
    public async Task AnularTerminacion_LanzaReglaDeNegocioDeclinadaException_CuandoLaTerminacionYaFueAnuladaAntes()
    {
        DadoUnColaboradorConTerminacionYaAnulada(FechaEfectivaTerminacion);

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // Tras un reingreso, la terminacion ANTERIOR queda congelada: anularla reabriria una vinculacion
    // teniendo otra abierta (no-solape).
    [Fact]
    public async Task AnularTerminacion_LanzaReglaDeNegocioDeclinadaException_CuandoLaUltimaVinculacionNacioDeUnReingresoSinTerminar()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada(CodigoVinculacionReingreso, fechaInicioReingreso));

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = CodigoVinculacionReingreso });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionReingreso);
    }

    // Sin Given: el stream no existe. Then sin eventos demuestra que no se escribio nada.
    [Fact]
    public async Task AnularTerminacion_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{AnularTerminacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
