// Issue #457: tests del endpoint HTTP PUT sedes/{codigo}/nombre (modificar nombre de sede).
// MEF-ADR-0004: KeyNotFoundException -> 404, exito -> 202. Precedente: CorregirNombresFunction.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static ModificarNombreSedeBody BodyValido() => new("Sede Renombrada");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: PUT exitoso retorna 202 Accepted
    [Fact]
    public async Task ModificarNombreSede_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4: PUT sobre una sede inexistente retorna 404 Not Found
    [Fact]
    public async Task ModificarNombreSede_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(BodyValido());
        var router = new FakeCommandRouter(lanzarKeyNotFoundException: true);
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-2: PUT con body invalido (nombre vacio) retorna 400 Bad Request
    [Fact]
    public async Task ModificarNombreSede_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

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
    private readonly bool _lanzarKeyNotFound;

    public FakeCommandRouter(bool lanzarKeyNotFoundException = false) =>
        _lanzarKeyNotFound = lanzarKeyNotFoundException;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_lanzarKeyNotFound)
            throw new KeyNotFoundException("La sede no existe");

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
