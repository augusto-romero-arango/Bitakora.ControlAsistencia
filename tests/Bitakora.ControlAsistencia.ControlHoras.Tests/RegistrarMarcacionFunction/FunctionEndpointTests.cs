using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

/// <summary>
/// Tests del endpoint HTTP POST control-horas/marcaciones: 201 sin Location -- la marcacion no
/// tiene GET canonico ni id en el comando.
/// </summary>
public class FunctionEndpointTests
{
    private static RegistrarMarcacion ComandoValido() =>
        new("EMP-001", new DateTime(2026, 3, 15, 8, 9, 59), "ENTRADA", "DEV-001");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // Cubre tanto marcacion nueva como duplicado silencioso: el endpoint no los distingue
    // porque el handler retorna sin lanzar excepcion en ambos casos.
    [Fact]
    public async Task RegistrarMarcacion_Retorna201SinLocation_CuandoHandlerRetornaSinExcepcion()
    {
        var validator = new FakeRequestValidatorMarcacion(ComandoValido());
        var router = new FakeCommandRouterMarcacion();
        var endpoint = new FunctionEndpoint(validator, router);

        var result = await endpoint.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>()
            .Which.Location.Should().BeNull();
    }

    [Fact]
    public async Task DebeRetornar400_CuandoRequestEsInvalido()
    {
        var error = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidatorMarcacion(error: error);
        var router = new FakeCommandRouterMarcacion();
        var endpoint = new FunctionEndpoint(validator, router);

        var result = await endpoint.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

internal class FakeRequestValidatorMarcacion : IRequestValidator
{
    private readonly RegistrarMarcacion? _comando;
    private readonly IActionResult? _error;

    public FakeRequestValidatorMarcacion(
        RegistrarMarcacion? comando = null,
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

// Siempre completa: simula por igual la marcacion nueva y el duplicado silencioso.
internal class FakeCommandRouterMarcacion : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => Task.CompletedTask;

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
