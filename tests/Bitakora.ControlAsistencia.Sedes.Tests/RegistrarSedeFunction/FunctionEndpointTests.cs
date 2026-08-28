// Issue #456: tests del endpoint HTTP POST sedes (registrar sede).
// MEF-ADR-0004: InvalidOperationException -> 409, exito -> 202. Precedente: RegistrarColaboradorFunction.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RegistrarSedeFunction;

public class FunctionEndpointTests
{
    private static RegistrarSede ComandoValido() =>
        new("SEDE-001", "Sede Principal", "Bogota", "Calle 100 # 10-20");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task RegistrarSede_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<RegistrarSede>(ComandoValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-5: POST con codigo ya registrado retorna 409 Conflict
    [Fact]
    public async Task RegistrarSede_Retorna409_CuandoCodigoYaExiste()
    {
        var validator = new FakeRequestValidator<RegistrarSede>(ComandoValido());
        var router = new FakeCommandRouter(lanzarInvalidOperationException: true);
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-3/CA-4: POST con JSON invalido o campos faltantes/invalidos retorna 400 Bad Request
    [Fact]
    public async Task RegistrarSede_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<RegistrarSede>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

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
    private readonly bool _lanzarInvalidOperation;

    public FakeCommandRouter(bool lanzarInvalidOperationException = false) =>
        _lanzarInvalidOperation = lanzarInvalidOperationException;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_lanzarInvalidOperation)
            throw new InvalidOperationException("La sede ya existe");

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
