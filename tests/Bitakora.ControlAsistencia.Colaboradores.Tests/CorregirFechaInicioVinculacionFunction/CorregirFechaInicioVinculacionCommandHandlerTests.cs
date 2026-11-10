using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionCommandHandlerTests
    : CommandHandlerAsyncTest<CorregirFechaInicioVinculacion>
{
    private const string NumeroValido = "79543210";

    // Oraculo independiente: literal, nunca derivado de ComputarStreamId -- si se derivara, un cambio
    // de formato de la clave se auto-validaria.
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

    private static CorregirFechaInicioVinculacion ComandoCon(string codigo, DateOnly fechaCorregida) => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: NumeroValido,
        Codigo: codigo,
        FechaCorregida: fechaCorregida);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    private static ColaboradorRegistrado ColaboradorRegistradoValido() =>
        new(IdentificacionValida(), NombreValido());

    private static VinculacionIniciada VinculacionIniciadaOriginal() =>
        new(CodigoVinculacionOriginal, FechaInicioOriginal);

    private void DadoUnColaboradorConVinculacionAbierta() =>
        Given(StreamIdEsperado, ColaboradorRegistradoValido(), VinculacionIniciadaOriginal());

    private void DadoUnColaboradorConVinculacionTerminada() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal));

    // La ultima vinculacion (el reingreso) es la que se corrige; la original es la anterior contra la
    // que se ejerce la no-solape.
    private void DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingreso));

    // Unico estado en que las dos reglas acotan la fecha a la vez:
    // FechaEfectivaTerminacionOriginal < fecha <= FechaEfectivaTerminacionReingreso.
    private void DadoUnColaboradorConReingresoYaTerminado() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingreso),
            new VinculacionTerminada(FechaEfectivaTerminacionReingreso));

    // La vinculacion anterior a la ultima es la del primer reingreso, no la original.
    private void DadoUnColaboradorConDosReingresos() =>
        Given(StreamIdEsperado,
            ColaboradorRegistradoValido(),
            VinculacionIniciadaOriginal(),
            new VinculacionTerminada(FechaEfectivaTerminacionOriginal),
            new VinculacionIniciada(CodigoReingreso, FechaInicioReingreso),
            new VinculacionTerminada(FechaEfectivaTerminacionReingreso),
            new VinculacionIniciada(CodigoSegundoReingreso, FechaInicioSegundoReingreso));

    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoLaUltimaVinculacionEstaAbierta()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);

        await WhenAsync(ComandoCon(CodigoVinculacionOriginal, fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoTipoYNumeroLleganSinNormalizar()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);
        var comandoSinNormalizar = ComandoCon(CodigoVinculacionOriginal, fechaCorregida) with
        {
            TipoIdentificacion = "cc",
            NumeroIdentificacion = "  79543210  "
        };

        await WhenAsync(comandoSinNormalizar);

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoElCodigoNoCorrespondeALaVinculacionVigente()
    {
        DadoUnColaboradorConVinculacionAbierta();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);

        var act = async () => await WhenAsync(ComandoCon("COL-999", fechaCorregida));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    // El codigo se evalua antes que la idempotencia: con codigo equivocado se rechaza aunque la fecha
    // sea igual a la actual.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaReglaDeNegocioDeclinadaExceptionPorCodigo_CuandoElCodigoNoCorrespondeYLaFechaEsIgualALaActual()
    {
        DadoUnColaboradorConVinculacionAbierta();

        var act = async () => await WhenAsync(ComandoCon("COL-999", FechaInicioOriginal));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.CodigoNoCorresponde}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsAnteriorALaFechaEfectivaPropia()
    {
        DadoUnColaboradorConVinculacionTerminada();
        var fechaCorregida = FechaInicioOriginal.AddDays(-5);

        await WhenAsync(ComandoCon(CodigoVinculacionOriginal, fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacionOriginal);
    }

    // Igual a la FechaEfectiva propia es valido: vinculacion de un solo dia, como en TerminarVinculacion.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsIgualALaFechaEfectivaPropia()
    {
        DadoUnColaboradorConVinculacionTerminada();

        await WhenAsync(ComandoCon(CodigoVinculacionOriginal, FechaEfectivaTerminacionOriginal));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(FechaEfectivaTerminacionOriginal));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaEfectivaTerminacionOriginal);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaCorregidaEsPosteriorALaFechaEfectivaPropia()
    {
        DadoUnColaboradorConVinculacionTerminada();

        var act = async () => await WhenAsync(
            ComandoCon(CodigoVinculacionOriginal, FechaEfectivaTerminacionOriginal.AddDays(1)));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaPosteriorATerminacionPropia}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoLaUltimaVinculacionEsUnReingresoAbierto()
    {
        DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto();
        var fechaCorregida = FechaEfectivaTerminacionOriginal.AddDays(2); // 2026-06-03

        await WhenAsync(ComandoCon(CodigoReingreso, fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
    }

    // El dia de la fecha efectiva pertenece a la vinculacion que termino: el mismo dia se rechaza
    // (misma frontera que IniciarVinculacion).
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaCorregidaEsIgualALaFechaEfectivaDeLaVinculacionAnterior()
    {
        DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto();

        var act = async () => await WhenAsync(
            ComandoCon(CodigoReingreso, FechaEfectivaTerminacionOriginal));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioReingreso);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaCorregidaEsAnteriorALaFechaEfectivaDeLaVinculacionAnterior()
    {
        DadoUnColaboradorConVinculacionAnteriorYReingresoAbierto();

        var act = async () => await WhenAsync(
            ComandoCon(CodigoReingreso, FechaEfectivaTerminacionOriginal.AddDays(-1)));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioReingreso);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_EmiteFechaInicioVinculacionCorregida_CuandoLaFechaCaeEntreLaTerminacionAnteriorYLaPropia()
    {
        DadoUnColaboradorConReingresoYaTerminado();
        // 2026-06-11: dentro de (2026-06-01, 2026-09-01] y distinta de la actual (2026-06-02), que
        // seria idempotencia y no ejercitaria ninguna de las dos reglas.
        var fechaCorregida = FechaEfectivaTerminacionOriginal.AddDays(10);

        await WhenAsync(ComandoCon(CodigoReingreso, fechaCorregida));

        Then(StreamIdEsperado, new FechaInicioVinculacionCorregida(fechaCorregida));
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, fechaCorregida);
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaTerminacionReingreso);
    }

    // La no-solape se evalua contra la terminacion inmediatamente anterior (2026-09-01), no la original
    // (2026-06-01): si se congelara en la primera, quedarian dos vinculaciones solapadas.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaReglaDeNegocioDeclinadaException_CuandoFechaCorregidaSolapaElReingresoPrevioYNoLaVinculacionOriginal()
    {
        DadoUnColaboradorConDosReingresos();
        var fechaCorregida = new DateOnly(2026, 7, 1); // > 2026-06-01 pero < 2026-09-01

        var act = async () => await WhenAsync(ComandoCon(CodigoSegundoReingreso, fechaCorregida));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.ReglaDeNegocioDeclinadaException>()
            .WithMessage(
                $"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaSolapaVinculacionAnterior}*");
        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioSegundoReingreso);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_NoEmiteEvento_CuandoFechaCorregidaEsIgualALaActual()
    {
        DadoUnColaboradorConVinculacionAbierta();

        await WhenAsync(ComandoCon(CodigoVinculacionOriginal, FechaInicioOriginal));

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    // La idempotencia se evalua antes que la coherencia interna, aunque la vinculacion ya este terminada.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_NoEmiteEvento_CuandoFechaCorregidaEsIgualALaActualYLaVinculacionEstaTerminada()
    {
        DadoUnColaboradorConVinculacionTerminada();

        await WhenAsync(ComandoCon(CodigoVinculacionOriginal, FechaInicioOriginal));

        Then(StreamIdEsperado);
        And<ColaboradorAggregateRoot, DateOnly>(
            StreamIdEsperado, c => c.FechaInicioVinculacionVigente, FechaInicioOriginal);
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(ComandoCon(CodigoVinculacionOriginal, FechaInicioOriginal));

        await act.Should().ThrowExactlyAsync<Bitakora.ControlAsistencia.Colaboradores.Infraestructura.RecursoNoEncontradoException>()
            .WithMessage($"*{CorregirFechaInicioVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamIdEsperado);
    }
}
