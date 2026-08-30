// Issue #498: tests del endpoint HTTP POST /programacion/cancelaciones.
// Verifica el mapeo de excepciones del handler a respuestas HTTP:
// - InvalidOperationException -> 409 (solicitud duplicada)
// - Exito -> 202 Accepted
// - Error de validacion -> 400 Bad Request

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarProgramacionFunction;

public class FunctionEndpointTests
{
    private static CancelarProgramacion ComandoValido() => new(
        Guid.NewGuid(),
        new ColaboradorSolicitado("CC-12345678", "E001", "Juan Perez"),
        [new DateOnly(2026, 4, 7)]);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task DebeRetornar202_CuandoComandoEsValido()
    {
        var validator = new FakeCancelacionRequestValidator(ComandoValido());
        var router = new FakeCancelacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-3: falla de validacion retorna 400 Bad Request
    [Fact]
    public async Task DebeRetornar400_CuandoFallaValidacion()
    {
        var errorDeValidacion = new BadRequestObjectResult("Campos requeridos faltantes");
        var validator = new FakeCancelacionRequestValidator(error: errorDeValidacion);
        var router = new FakeCancelacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-2: solicitud ya existe retorna 409 Conflict
    [Fact]
    public async Task DebeRetornar409_CuandoSolicitudYaExiste()
    {
        var validator = new FakeCancelacionRequestValidator(ComandoValido());
        var router = new FakeCancelacionCommandRouter(
            lanzar: new InvalidOperationException("La solicitud ya existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

internal class FakeCancelacionRequestValidator : IRequestValidator
{
    private readonly CancelarProgramacion? _comando;
    private readonly IActionResult? _error;

    public FakeCancelacionRequestValidator(
        CancelarProgramacion? comando = null,
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

internal class FakeCancelacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCancelacionCommandRouter(Exception? lanzar = null)
    {
        _excepcion = lanzar;
    }

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
