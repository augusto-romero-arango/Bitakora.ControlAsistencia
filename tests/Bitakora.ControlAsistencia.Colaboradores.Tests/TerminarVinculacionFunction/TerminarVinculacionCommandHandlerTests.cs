// CodigoNoCorresponde es 409, no 404: es conflicto con la vinculacion vigente, no un recurso inexistente.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

public class TerminarVinculacionCommandHandlerTests : CommandHandlerAsyncTest<TerminarVinculacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente: literal, nunca derivado de ComputarStreamId -- si se derivara, un cambio
    // de formato de la clave se auto-validaria.
    private const string StreamIdEsperado = "CC-79543210";

    private const string CodigoVinculacionVigente = "COL-001";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaValida = new(2026, 6, 1);

    protected override ICommandHandlerAsync<TerminarVinculacion> Handler =>
        new TerminarVinculacionCommandHandler(EventStore);

    private static TerminarVinculacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Codigo: CodigoVinculacionVigente,
        FechaEfectiva: FechaEfectivaValida);

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

    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoLaVinculacionEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoValido());

        Then(StreamIdEsperado, new VinculacionTerminada(FechaEfectivaValida));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaValida);
    }

    // Sin validacion contra el reloj del servidor en ninguna direccion.
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

    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoElCodigoNoCorrespondeALaVinculacionVigente()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = "COL-999" });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // El direccionamiento precede a las reglas de estado: un comando dirigido a la vinculacion
    // equivocada no debe filtrar que la vigente ya esta terminada.
    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaExceptionPorCodigo_CuandoElCodigoNoCorrespondeYLaVinculacionYaEstaTerminada()
    {
        var fechaTerminacionPrevia = new DateOnly(2026, 3, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionPrevia));

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = "COL-999" });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaTerminacionPrevia);
    }

    // La comparacion del codigo es exacta: se preserva su case.
    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoElCodigoDifiereSoloEnMayusculas()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoValido() with { Codigo = CodigoVinculacionVigente.ToLowerInvariant() });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoLaVinculacionYaTieneTerminacionRegistrada()
    {
        var fechaTerminacionPrevia = new DateOnly(2026, 3, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionPrevia));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaTerminacionPrevia);
    }

    // "Ya terminada" se evalua solo con la historia del stream, sin reloj.
    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoYaExisteUnPreavisoConFechaFutura()
    {
        var fechaPreavisoFutura = new DateOnly(2030, 1, 1);
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaPreavisoFutura));

        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, fechaPreavisoFutura);
    }

    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaEfectivaEsAnteriorAFechaInicio()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaAnterior = FechaInicioVinculacionVigente.AddDays(-1);

        var act = async () => await WhenAsync(ComandoValido() with { FechaEfectiva = fechaAnterior });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.FechaAnteriorAInicio}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoFechaEfectivaEsIgualAFechaInicio()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoValido() with { FechaEfectiva = FechaInicioVinculacionVigente });

        Then(StreamIdEsperado, new VinculacionTerminada(FechaInicioVinculacionVigente));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaInicioVinculacionVigente);
    }

    // Terminar un reingreso funciona porque Apply(VinculacionIniciada) reabre la vinculacion: sin ese
    // reset, chocaria con la terminacion heredada y responderia 409.
    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminada_CuandoLaVinculacionVigenteNacioDeUnReingreso()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        const string codigoReingreso = "COL-002";
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada(codigoReingreso, fechaInicioReingreso));

        await WhenAsync(ComandoValido() with { Codigo = codigoReingreso });

        Then(StreamIdEsperado, new VinculacionTerminada(FechaEfectivaValida));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaValida);
    }

    // La ventana de fechas validas se mueve con el reingreso: posterior al inicio original pero anterior
    // al del reingreso es duracion negativa (409).
    [Fact]
    public async Task TerminarVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaEfectivaEsAnteriorAlInicioDelReingreso()
    {
        var fechaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);
        const string codigoReingreso = "COL-002";
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaVigente(),
            new VinculacionTerminada(fechaTerminacionAnterior),
            new VinculacionIniciada(codigoReingreso, fechaInicioReingreso));

        var act = async () => await WhenAsync(
            ComandoValido() with { Codigo = codigoReingreso, FechaEfectiva = fechaInicioReingreso.AddDays(-1) });

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.FechaAnteriorAInicio}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, null);
    }

    // Sin And<>: el aggregate no existe en el TestStore.
    [Fact]
    public async Task TerminarVinculacion_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoValido());

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{TerminarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
