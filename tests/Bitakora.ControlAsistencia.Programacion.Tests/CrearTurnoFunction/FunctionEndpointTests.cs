using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

public class FunctionEndpointTests
{
    private static CrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    private static CrearTurno ComandoValido() =>
        new(Guid.NewGuid(), "Turno Manana", [FranjaDiurnaSimple()]);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // La transaccion (UnitOfWorkMiddleware + AutoApplyTransactions) confirma antes de responder:
    // 201 con Location, nunca 202.
    [Fact]
    public async Task CrearTurno_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var comando = ComandoValido();
        var validator = new FakeRequestValidator<CrearTurno>(comando);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>()
            .Which.Location.Should().Be($"/api/programacion/turnos/{comando.TurnoId}");
    }

    [Fact]
    public async Task CrearTurno_Retorna409_CuandoTurnoYaExiste()
    {
        var validator = new FakeRequestValidator<CrearTurno>(ComandoValido());
        var router = new FakeCommandRouter(new RecursoYaExisteException(CrearTurnoCommandHandler.Mensajes.TurnoYaExiste));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearTurno_Retorna409_CuandoNombreEstaDuplicado()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurno>(ComandoValido()),
            new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(CrearTurnoCommandHandler.Mensajes.NombreDuplicado)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearTurno_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurno>(ComandoValido()), new FakeCommandRouter(fallo));

        var act = async () => await function.Run(FakeHttpRequest(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }

    [Fact]
    public async Task DebeRetornar400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<CrearTurno>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DebeRetornar400ConMensajes_CuandoFranjasDelComandoSonInvalidas()
    {
        var validator = new FakeRequestValidator<CrearTurno>(ComandoValido());
        var erroresDeNegocio = new ArgumentException[] { new("La franja ordinaria es invalida") };
        var router = new FakeCommandRouter(new AggregateException(erroresDeNegocio));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// Fakes manuales - NO NSubstitute.
internal class FakeRequestValidator<TComando> : IRequestValidator
{
    private readonly TComando? _comando;
    private readonly IActionResult? _error;

    public FakeRequestValidator(TComando? comando = default, IActionResult? error = null)
    {
        _comando = comando;
        _error = error;
    }

    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(
        HttpRequest req, CancellationToken ct)
    {
        if (_error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, _error));

        if (_comando is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

internal class FakeCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCommandRouter(Exception? excepcion = null) => _excepcion = excepcion;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_excepcion is not null) throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
