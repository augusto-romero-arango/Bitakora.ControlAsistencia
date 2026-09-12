using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

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

    // Persiste SolicitudCancelacion antes de responder -> 201 Created sin Location (la solicitud
    // no tiene GET canonico; el efecto en ControlHoras es posterior al commit).
    [Fact]
    public async Task CancelarProgramacion_Retorna201SinLocation_CuandoComandoEsValido()
    {
        var validator = new FakeCancelacionRequestValidator(ComandoValido());
        var router = new FakeCancelacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>()
            .Which.Location.Should().BeNull();
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
