// HU-105: Tests del FunctionEndpoint HTTP POST RegistrarMarcacion

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

/// <summary>
/// Tests del endpoint HTTP POST control-horas/marcaciones.
/// Verifica que el endpoint mapea correctamente los resultados del handler a respuestas HTTP.
/// CA-6: responde 202 Accepted tanto en creacion exitosa como en duplicado silencioso.
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

    // CA-6, CA-7: POST exitoso retorna 202 Accepted.
    // Cubre tanto marcacion nueva como duplicado silencioso: el endpoint no los distingue
    // porque el handler retorna sin lanzar excepcion en ambos casos.
    [Fact]
    public async Task DebeRetornar202_CuandoHandlerRetornaSinExcepcion()
    {
        var validator = new FakeRequestValidatorMarcacion(ComandoValido());
        var router = new FakeCommandRouterMarcacion();
        var endpoint = new FunctionEndpoint(validator, router);

        var result = await endpoint.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-6: request invalido (validacion falla) retorna 400 Bad Request
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

/// <summary>
/// Fake de IRequestValidator para RegistrarMarcacion.
/// Retorna un comando pre-configurado o un error segun lo que se pase en el constructor.
/// </summary>
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

/// <summary>
/// Fake de ICommandRouter para RegistrarMarcacion.
/// Siempre completa exitosamente (simula tanto nueva marcacion como duplicado silencioso).
/// </summary>
internal class FakeCommandRouterMarcacion : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => Task.CompletedTask;

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
