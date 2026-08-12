// Issue #354: tests del endpoint HTTP POST Colaboradores/Terminaciones/Anulaciones (anular
// terminacion). CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409, KeyNotFoundException
// -> 404, exito -> 202. Precedente: TerminarVinculacionFunction.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

public class FunctionEndpointTests
{
    private static AnularTerminacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task AnularTerminacion_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeAnularTerminacionRequestValidator(ComandoValido());
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-3/CA-4: violacion de la unica regla (vinculacion abierta) retorna 409 Conflict.
    [Fact]
    public async Task AnularTerminacion_Retorna409_CuandoLaVinculacionEstaAbierta()
    {
        var validator = new FakeAnularTerminacionRequestValidator(ComandoValido());
        var router = new FakeAnularTerminacionCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador no tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-5: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task AnularTerminacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeAnularTerminacionRequestValidator(ComandoValido());
        var router = new FakeAnularTerminacionCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-6: request invalida (sin identificacion, tipo fuera de la lista cerrada) retorna 400 Bad
    // Request
    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeAnularTerminacionRequestValidator(error: errorDeValidacion);
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator. Retorna un comando pre-configurado o un error segun lo
/// que se le pase en el constructor.
/// </summary>
internal class FakeAnularTerminacionRequestValidator : IRequestValidator
{
    private readonly AnularTerminacion? _comando;
    private readonly IActionResult? _error;

    public FakeAnularTerminacionRequestValidator(
        AnularTerminacion? comando = null,
        IActionResult? error = null)
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

/// <summary>
/// Fake configurable de ICommandRouter. Puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
internal class FakeAnularTerminacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeAnularTerminacionCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
